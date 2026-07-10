using System;
using System.Collections.Generic;

namespace Nexa.App.Terminal;

/// <summary>터미널 셀 하나 — 문자 + 전경/배경색(ARGB) + 굵게/반전/흐리게(faint).</summary>
public struct TermCell
{
    public char Ch;
    public uint Fg;
    public uint Bg;
    public bool Bold;
    public bool Reverse;
    public bool Faint;   // SGR 2 — PSReadLine 인라인 예측(history) 등이 연한 회색으로 표시하는 데 사용.
}

/// <summary>
/// VT/ANSI 파서 + 화면 버퍼(BP-T2) — ConPTY가 내보내는 VT 시퀀스를 해석해 <b>셀 그리드</b>(문자·색·속성)로 유지한다.
/// 지원: 출력 문자·CR/LF/BS/HT, SGR(16/256/트루컬러·굵게·반전·리셋), 커서 이동(CUP/CUU·D·F·B/CHA/VPA),
/// 지우기(ED/EL), 줄 스크롤(스크롤백 보존). 렌더는 <c>TerminalView</c>가 <see cref="Lines"/>로 수행.
/// </summary>
public sealed class VtScreen
{
    public const uint DefaultFg = 0xFFE6E6E6;
    public const uint DefaultBg = 0xFF0C0F12;

    private int _cols, _rows;
    private TermCell[][] _screen = Array.Empty<TermCell[]>();
    private readonly List<TermCell[]> _scrollback = new();
    private const int MaxScrollback = 800;

    private int _cx, _cy;
    private int _savedCx, _savedCy;   // DECSC/DECRC(ESC 7/8)·CSI s/u 커서 저장/복원
    private int _top, _bottom;        // 스크롤 마진(DECSTBM, 포함 범위) — 기본 전체 화면
    private uint _fg = DefaultFg, _bg = DefaultBg;
    private bool _bold, _reverse, _faint;

    // 파서 상태
    private enum S { Ground, Esc, Csi, Osc }
    private S _state = S.Ground;
    private readonly List<int> _pars = new();
    private int _cur = -1;         // 현재 파라미터 누적(-1=없음)

    public VtScreen(int cols, int rows) => Resize(cols, rows);

    public int Cols => _cols;
    public int Rows => _rows;

    /// <summary>커서 열(0-기준, 가시 화면 좌표). 렌더가 캐럿 위치 계산에 사용.</summary>
    public int CursorCol => _cx;

    /// <summary>커서 행(0-기준, 가시 화면 내). 절대 라인 인덱스 = <see cref="ScrollbackCount"/> + 이 값.</summary>
    public int CursorRow => _cy;

    /// <summary>스크롤백 라인 수(<see cref="Lines"/>에서 가시 화면 앞에 오는 라인 수).</summary>
    public int ScrollbackCount => _scrollback.Count;

    /// <summary>절대 라인(스크롤백+화면) 범위의 텍스트 추출(양끝 포함) — 마우스 선택 복사용.
    /// 전각 연속 셀('\0')은 건너뛰고, 각 줄 우측 공백은 트림, 줄 구분은 CRLF.</summary>
    public string GetText(int startLine, int startCol, int endLine, int endCol)
    {
        int count = LineCount;
        if (count == 0)
        {
            return string.Empty;
        }
        startLine = Math.Clamp(startLine, 0, count - 1);
        endLine = Math.Clamp(endLine, 0, count - 1);
        var sb = new System.Text.StringBuilder();
        for (int li = startLine; li <= endLine; li++)
        {
            TermCell[] row = LineAt(li);
            int c0 = li == startLine ? Math.Max(0, startCol) : 0;
            int c1 = li == endLine ? Math.Min(row.Length - 1, endCol) : row.Length - 1;
            var line = new System.Text.StringBuilder();
            for (int c = c0; c <= c1 && c < row.Length; c++)
            {
                char ch = row[c].Ch;
                if (ch != '\0')
                {
                    line.Append(ch);
                }
            }
            sb.Append(line.ToString().TrimEnd());
            if (li < endLine)
            {
                sb.Append("\r\n");
            }
        }
        return sb.ToString();
    }

    /// <summary>총 라인 수(스크롤백 + 현재 화면) — 목록 실체화 없이.</summary>
    public int LineCount => _scrollback.Count + _rows;

    /// <summary>절대 라인 인덱스의 셀 배열(스크롤백 → 화면 순, 0 ≤ index &lt; <see cref="LineCount"/>).
    /// 렌더가 매 프레임(~30fps) 라인 목록을 새 List로 실체화하던 것을 대체(감사 004 — gen0 churn 제거).</summary>
    public TermCell[] LineAt(int index)
        => index < _scrollback.Count ? _scrollback[index] : _screen[index - _scrollback.Count];

    public void Resize(int cols, int rows)
    {
        cols = Math.Max(1, cols);
        rows = Math.Max(1, rows);
        if (cols == _cols && rows == _rows)
        {
            return;
        }
        var next = new TermCell[rows][];
        for (int r = 0; r < rows; r++)
        {
            next[r] = BlankRow(cols);
            if (r < _rows && _screen.Length > r)
            {
                int copy = Math.Min(cols, _cols);
                Array.Copy(_screen[r], next[r], copy);
            }
        }
        _screen = next;
        _cols = cols;
        _rows = rows;
        _cx = Math.Min(_cx, cols - 1);
        _cy = Math.Min(_cy, rows - 1);
        _top = 0;
        _bottom = rows - 1;   // 리사이즈 시 스크롤 마진 리셋(DECSTBM 관례)
    }

    private static TermCell[] MakeRow(int cols, uint fg, uint bg)
    {
        var row = new TermCell[cols];
        for (int i = 0; i < cols; i++)
        {
            row[i] = new TermCell { Ch = ' ', Fg = fg, Bg = bg };
        }
        return row;
    }

    private TermCell[] BlankRow(int cols) => MakeRow(cols, DefaultFg, DefaultBg);

    public void Feed(string data)
    {
        foreach (char ch in data)
        {
            switch (_state)
            {
                case S.Ground: Ground(ch); break;
                case S.Esc: Escape(ch); break;
                case S.Csi: Csi(ch); break;
                case S.Osc: Osc(ch); break;
            }
        }
    }

    private void Ground(char ch)
    {
        switch (ch)
        {
            case '\x1B': _state = S.Esc; break;
            case '\r': _cx = 0; break;
            case '\n': LineFeed(); break;
            case '\b': if (_cx > 0) { _cx--; } break;
            case '\t': _cx = Math.Min(_cols - 1, (_cx / 8 + 1) * 8); break;
            case '\a': break;   // BEL
            default:
                if (ch >= ' ')
                {
                    Put(ch);
                }
                break;
        }
    }

    private void Escape(char ch)
    {
        switch (ch)
        {
            case '[': _state = S.Csi; _pars.Clear(); _cur = -1; break;
            case ']': _state = S.Osc; break;
            case '(': case ')': case '*': case '+': _state = S.Ground; break;   // charset 지정 — 다음 1글자는 무시(간이)
            case 'M': ReverseIndex(); _state = S.Ground; break;
            case 'D': LineFeed(); _state = S.Ground; break;                       // IND — 아래로(최하단이면 스크롤)
            case 'E': _cx = 0; LineFeed(); _state = S.Ground; break;              // NEL — 다음 줄 처음
            case '7': _savedCx = _cx; _savedCy = _cy; _state = S.Ground; break;   // DECSC 커서 저장
            case '8': RestoreCursor(); _state = S.Ground; break;                  // DECRC 커서 복원
            case '=': case '>': _state = S.Ground; break;
            case 'c': FullReset(); _state = S.Ground; break;
            default: _state = S.Ground; break;
        }
    }

    private void Csi(char ch)
    {
        if (ch == '?')
        {
            return;   // private CSI(?) 마커 — 현재 미사용(?…h/l 등은 최종 바이트에서 무시)
        }
        if (ch >= '0' && ch <= '9')
        {
            _cur = (_cur < 0 ? 0 : _cur) * 10 + (ch - '0');
            return;
        }
        if (ch == ';')
        {
            _pars.Add(_cur < 0 ? 0 : _cur);
            _cur = -1;
            return;
        }
        // 중간 바이트(SP-/) 무시, 최종 바이트에서 디스패치
        if (ch >= 0x40 && ch <= 0x7E)
        {
            _pars.Add(_cur < 0 ? 0 : _cur);
            Dispatch(ch);
            _state = S.Ground;
        }
    }

    private void Osc(char ch)
    {
        // OSC 종료: BEL 또는 ST(ESC \). 여기선 BEL/ESC로 종료(간이 — 창 제목 등 무시).
        if (ch == '\a' || ch == '\x1B')
        {
            _state = ch == '\x1B' ? S.Esc : S.Ground;
        }
    }

    private int Par(int i, int def) => i < _pars.Count && _pars[i] > 0 ? _pars[i] : (i < _pars.Count && _pars[i] == 0 && def == 0 ? 0 : def);

    private void Dispatch(char final)
    {
        int p0 = _pars.Count > 0 ? _pars[0] : 0;
        switch (final)
        {
            case 'm': Sgr(); break;
            case 'H': case 'f':
                _cy = Math.Clamp((Par(0, 1)) - 1, 0, _rows - 1);
                _cx = Math.Clamp((Par(1, 1)) - 1, 0, _cols - 1);
                break;
            case 'A': _cy = Math.Max(0, _cy - Math.Max(1, p0)); break;
            case 'B': _cy = Math.Min(_rows - 1, _cy + Math.Max(1, p0)); break;
            case 'C': _cx = Math.Min(_cols - 1, _cx + Math.Max(1, p0)); break;
            case 'D': _cx = Math.Max(0, _cx - Math.Max(1, p0)); break;
            case 'G': _cx = Math.Clamp(Math.Max(1, p0) - 1, 0, _cols - 1); break;
            case 'd': _cy = Math.Clamp(Math.Max(1, p0) - 1, 0, _rows - 1); break;
            case 'E': _cy = Math.Min(_rows - 1, _cy + Math.Max(1, p0)); _cx = 0; break;   // CNL — 아래 n줄 처음
            case 'F': _cy = Math.Max(0, _cy - Math.Max(1, p0)); _cx = 0; break;           // CPL — 위 n줄 처음
            case 'S': ScrollUp(Math.Max(1, p0)); break;    // SU — 콘솔 스크롤(미구현 시 낡은 줄 잔존·커서 행 어긋남)
            case 'T': ScrollDown(Math.Max(1, p0)); break;  // SD
            case 'r':   // DECSTBM — 스크롤 마진 설정(미구현 시 영역 스크롤이 전체 화면과 어긋남, ls 등)
                _top = Math.Clamp(Par(0, 1) - 1, 0, _rows - 1);
                _bottom = Math.Clamp(Par(1, _rows) - 1, 0, _rows - 1);
                if (_bottom <= _top)
                {
                    _top = 0;
                    _bottom = _rows - 1;   // 무효 → 전체 화면
                }
                _cx = 0;
                _cy = 0;   // DECSTBM은 커서 홈(스펙)
                break;
            case 'J': EraseDisplay(p0); break;
            case 'K': EraseLine(p0); break;
            case 'L': InsertLines(Math.Max(1, p0)); break;
            case 'M': DeleteLines(Math.Max(1, p0)); break;
            case 'P': DeleteChars(Math.Max(1, p0)); break;
            case '@': InsertChars(Math.Max(1, p0)); break;
            case 'X': EraseChars(Math.Max(1, p0)); break;   // ECH — ConPTY가 라인 일부 지울 때 다용(잔상 방지 필수)
            case 's': _savedCx = _cx; _savedCy = _cy; break;   // 커서 저장
            case 'u': RestoreCursor(); break;                  // 커서 복원
            default: break;    // 미지원은 무시
        }
    }

    private void Put(char ch)
    {
        int w = IsWide(ch) ? 2 : 1;   // 셸(ConPTY)은 CJK 전각을 2칸으로 계산 — 버퍼도 동일하게 전진해야 커서가 맞는다.
        if (_cx + w > _cols)
        {
            _cx = 0;
            LineFeed();
        }
        _screen[_cy][_cx] = new TermCell { Ch = ch, Fg = _fg, Bg = _bg, Bold = _bold, Reverse = _reverse, Faint = _faint };
        if (w == 2 && _cx + 1 < _cols)
        {
            _screen[_cy][_cx + 1] = new TermCell { Ch = '\0', Fg = _fg, Bg = _bg };   // 연속(continuation) 셀 — 렌더는 스킵
        }
        _cx += w;
    }

    /// <summary>전각(2칸) 문자인가 — wcwidth 근사(한글·CJK·전각 기호). BMP 주요 범위만(간이).</summary>
    public static bool IsWide(char ch) =>
        (ch >= 0x1100 && ch <= 0x115F) ||   // Hangul Jamo
        (ch >= 0x2E80 && ch <= 0xA4CF) ||   // CJK Radicals~Yi
        (ch >= 0xAC00 && ch <= 0xD7A3) ||   // Hangul Syllables
        (ch >= 0xF900 && ch <= 0xFAFF) ||   // CJK Compat Ideographs
        (ch >= 0xFE30 && ch <= 0xFE4F) ||   // CJK Compat Forms
        (ch >= 0xFF00 && ch <= 0xFF60) ||   // Fullwidth Forms
        (ch >= 0xFFE0 && ch <= 0xFFE6);     // Fullwidth Signs

    private void LineFeed()
    {
        if (_cy == _bottom)
        {
            ScrollUp(1);   // 마진 하단에서의 LF = 영역 스크롤(전체 화면 마진이면 스크롤백 보존)
            return;
        }
        if (_cy < _rows - 1)
        {
            _cy++;
        }
    }

    /// <summary>스크롤 마진 영역을 <paramref name="n"/>줄 위로 스크롤(SU). 전체 화면 마진이면 맨 위 줄을
    /// 스크롤백으로 보존, 부분 마진(DECSTBM)이면 영역 내부만 이동(스크롤백 미보존 — 터미널 표준). 커서 불변.</summary>
    private void ScrollUp(int n)
    {
        bool full = _top == 0 && _bottom == _rows - 1;
        for (int k = 0; k < n; k++)
        {
            if (full)
            {
                _scrollback.Add(_screen[_top]);
            }
            for (int r = _top + 1; r <= _bottom; r++)
            {
                _screen[r - 1] = _screen[r];
            }
            _screen[_bottom] = BlankRow(_cols);
        }
        if (full && _scrollback.Count > MaxScrollback)
        {
            _scrollback.RemoveRange(0, _scrollback.Count - MaxScrollback);
        }
    }

    /// <summary>스크롤 마진 영역을 <paramref name="n"/>줄 아래로 스크롤(SD) — 영역 위는 빈 줄, 맨 아래는 버림. 커서 불변.</summary>
    private void ScrollDown(int n)
    {
        for (int k = 0; k < n; k++)
        {
            for (int r = _bottom; r > _top; r--)
            {
                _screen[r] = _screen[r - 1];
            }
            _screen[_top] = BlankRow(_cols);
        }
    }

    private void ReverseIndex()
    {
        if (_cy == _top)
        {
            ScrollDown(1);   // 마진 상단에서의 RI = 영역 아래로 스크롤
        }
        else if (_cy > 0)
        {
            _cy--;
        }
    }

    private void EraseDisplay(int mode)
    {
        switch (mode)
        {
            case 0:   // 커서 → 끝
                EraseLine(0);
                for (int r = _cy + 1; r < _rows; r++) { _screen[r] = BlankFilledRow(); }
                break;
            case 1:   // 시작 → 커서
                for (int r = 0; r < _cy; r++) { _screen[r] = BlankFilledRow(); }
                EraseLine(1);
                break;
            case 2:   // 전체
                for (int r = 0; r < _rows; r++) { _screen[r] = BlankFilledRow(); }
                break;
            case 3:   // 스크롤백 지우기
                _scrollback.Clear();
                break;
        }
    }

    private void EraseLine(int mode)
    {
        var row = _screen[_cy];
        int from = mode == 1 ? 0 : (mode == 2 ? 0 : _cx);
        int to = mode == 0 ? _cols - 1 : (mode == 2 ? _cols - 1 : _cx);
        for (int c = from; c <= to && c < _cols; c++)
        {
            row[c] = new TermCell { Ch = ' ', Fg = _fg, Bg = _bg };
        }
    }

    private TermCell[] BlankFilledRow() => MakeRow(_cols, _fg, _bg);

    private void InsertLines(int n)
    {
        for (int k = 0; k < n; k++)
        {
            for (int r = _rows - 1; r > _cy; r--) { _screen[r] = _screen[r - 1]; }
            _screen[_cy] = BlankFilledRow();
        }
    }

    private void DeleteLines(int n)
    {
        for (int k = 0; k < n; k++)
        {
            for (int r = _cy; r < _rows - 1; r++) { _screen[r] = _screen[r + 1]; }
            _screen[_rows - 1] = BlankFilledRow();
        }
    }

    /// <summary>ECH(CSI n X) — 커서 위치부터 n칸을 지운다(커서는 이동하지 않음). PSReadLine 백스페이스 재그리기 등.</summary>
    private void EraseChars(int n)
    {
        var row = _screen[_cy];
        int to = Math.Min(_cols, _cx + n);
        for (int c = _cx; c < to; c++)
        {
            row[c] = new TermCell { Ch = ' ', Fg = _fg, Bg = _bg };
        }
    }

    private void RestoreCursor()
    {
        _cx = Math.Clamp(_savedCx, 0, _cols - 1);
        _cy = Math.Clamp(_savedCy, 0, _rows - 1);
    }

    private void DeleteChars(int n)
    {
        var row = _screen[_cy];
        for (int c = _cx; c < _cols; c++)
        {
            row[c] = c + n < _cols ? row[c + n] : new TermCell { Ch = ' ', Fg = _fg, Bg = _bg };
        }
    }

    private void InsertChars(int n)
    {
        var row = _screen[_cy];
        for (int c = _cols - 1; c >= _cx; c--)
        {
            row[c] = c - n >= _cx ? row[c - n] : new TermCell { Ch = ' ', Fg = _fg, Bg = _bg };
        }
    }

    private void FullReset()
    {
        _fg = DefaultFg; _bg = DefaultBg; _bold = _reverse = _faint = false;
        _cx = _cy = 0;
        _top = 0;
        _bottom = _rows - 1;
        for (int r = 0; r < _rows; r++) { _screen[r] = BlankRow(_cols); }
    }

    // ── SGR (색/속성) ────────────────────────────────────────────────
    private void Sgr()
    {
        if (_pars.Count == 0)
        {
            _pars.Add(0);
        }
        for (int i = 0; i < _pars.Count; i++)
        {
            int p = _pars[i];
            switch (p)
            {
                case 0: _fg = DefaultFg; _bg = DefaultBg; _bold = _reverse = _faint = false; break;
                case 1: _bold = true; break;
                case 2: _faint = true; break;
                case 22: _bold = _faint = false; break;
                case 7: _reverse = true; break;
                case 27: _reverse = false; break;
                case 39: _fg = DefaultFg; break;
                case 49: _bg = DefaultBg; break;
                case 38:
                    if (i + 2 < _pars.Count && _pars[i + 1] == 5) { _fg = Color256(_pars[i + 2]); i += 2; }
                    else if (i + 4 < _pars.Count && _pars[i + 1] == 2) { _fg = Rgb(_pars[i + 2], _pars[i + 3], _pars[i + 4]); i += 4; }
                    break;
                case 48:
                    if (i + 2 < _pars.Count && _pars[i + 1] == 5) { _bg = Color256(_pars[i + 2]); i += 2; }
                    else if (i + 4 < _pars.Count && _pars[i + 1] == 2) { _bg = Rgb(_pars[i + 2], _pars[i + 3], _pars[i + 4]); i += 4; }
                    break;
                default:
                    if (p >= 30 && p <= 37) { _fg = Ansi16(p - 30); }
                    else if (p >= 40 && p <= 47) { _bg = Ansi16(p - 40); }
                    else if (p >= 90 && p <= 97) { _fg = Ansi16(p - 90 + 8); }
                    else if (p >= 100 && p <= 107) { _bg = Ansi16(p - 100 + 8); }
                    break;
            }
        }
    }

    // Campbell(Windows Terminal 기본) 16색 팔레트
    private static readonly uint[] Palette16 =
    {
        0xFF0C0C0C, 0xFFC50F1F, 0xFF13A10E, 0xFFC19C00, 0xFF0037DA, 0xFF881798, 0xFF3A96DD, 0xFFCCCCCC,
        0xFF767676, 0xFFE74856, 0xFF16C60C, 0xFFF9F1A5, 0xFF3B78FF, 0xFFB4009E, 0xFF61D6D6, 0xFFF2F2F2,
    };

    private static uint Ansi16(int i) => Palette16[Math.Clamp(i, 0, 15)];

    private static uint Color256(int n)
    {
        n = Math.Clamp(n, 0, 255);
        if (n < 16) { return Ansi16(n); }
        if (n < 232)
        {
            int c = n - 16;
            int r = c / 36, g = (c % 36) / 6, b = c % 6;
            int Conv(int v) => v == 0 ? 0 : 55 + v * 40;
            return Rgb(Conv(r), Conv(g), Conv(b));
        }
        int gray = 8 + (n - 232) * 10;
        return Rgb(gray, gray, gray);
    }

    private static uint Rgb(int r, int g, int b) =>
        0xFF000000u | ((uint)(r & 0xFF) << 16) | ((uint)(g & 0xFF) << 8) | (uint)(b & 0xFF);
}
