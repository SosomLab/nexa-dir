using System;
using System.Collections.Generic;

namespace Nexa.App.Terminal;

/// <summary>터미널 셀 하나 — 문자 + 전경/배경색(ARGB) + 굵게/반전.</summary>
public struct TermCell
{
    public char Ch;
    public uint Fg;
    public uint Bg;
    public bool Bold;
    public bool Reverse;
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
    private uint _fg = DefaultFg, _bg = DefaultBg;
    private bool _bold, _reverse, _faint;

    // 파서 상태
    private enum S { Ground, Esc, Csi, Osc }
    private S _state = S.Ground;
    private readonly List<int> _pars = new();
    private int _cur = -1;         // 현재 파라미터 누적(-1=없음)
    private bool _priv;            // CSI ? private

    public VtScreen(int cols, int rows) => Resize(cols, rows);

    public int Cols => _cols;
    public int Rows => _rows;

    /// <summary>렌더용 라인 목록(스크롤백 + 현재 화면). 각 라인은 셀 배열.</summary>
    public IReadOnlyList<TermCell[]> Lines
    {
        get
        {
            var list = new List<TermCell[]>(_scrollback.Count + _rows);
            list.AddRange(_scrollback);
            for (int r = 0; r < _rows; r++)
            {
                list.Add(_screen[r]);
            }
            return list;
        }
    }

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
    }

    private TermCell[] BlankRow(int cols)
    {
        var row = new TermCell[cols];
        for (int i = 0; i < cols; i++)
        {
            row[i] = new TermCell { Ch = ' ', Fg = DefaultFg, Bg = DefaultBg };
        }
        return row;
    }

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
            case '[': _state = S.Csi; _pars.Clear(); _cur = -1; _priv = false; break;
            case ']': _state = S.Osc; break;
            case '(': case ')': case '*': case '+': _state = S.Ground; break;   // charset 지정 — 다음 1글자는 무시(간이)
            case 'M': ReverseIndex(); _state = S.Ground; break;
            case '=': case '>': _state = S.Ground; break;
            case 'c': FullReset(); _state = S.Ground; break;
            default: _state = S.Ground; break;
        }
    }

    private void Csi(char ch)
    {
        if (ch == '?')
        {
            _priv = true;
            return;
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
            case 'J': EraseDisplay(p0); break;
            case 'K': EraseLine(p0); break;
            case 'L': InsertLines(Math.Max(1, p0)); break;
            case 'M': DeleteLines(Math.Max(1, p0)); break;
            case 'P': DeleteChars(Math.Max(1, p0)); break;
            case '@': InsertChars(Math.Max(1, p0)); break;
            case 's': break;   // 커서 저장(간이 무시)
            case 'u': break;
            default: break;    // 미지원은 무시
        }
    }

    private void Put(char ch)
    {
        if (_cx >= _cols)
        {
            _cx = 0;
            LineFeed();
        }
        _screen[_cy][_cx] = new TermCell { Ch = ch, Fg = _fg, Bg = _bg, Bold = _bold, Reverse = _reverse };
        _cx++;
    }

    private void LineFeed()
    {
        _cy++;
        if (_cy >= _rows)
        {
            _cy = _rows - 1;
            _scrollback.Add(_screen[0]);
            for (int r = 1; r < _rows; r++)
            {
                _screen[r - 1] = _screen[r];
            }
            _screen[_rows - 1] = BlankRow(_cols);
            if (_scrollback.Count > MaxScrollback)
            {
                _scrollback.RemoveRange(0, _scrollback.Count - MaxScrollback);
            }
        }
    }

    private void ReverseIndex()
    {
        if (_cy > 0)
        {
            _cy--;
        }
        else
        {
            for (int r = _rows - 1; r > 0; r--)
            {
                _screen[r] = _screen[r - 1];
            }
            _screen[0] = BlankRow(_cols);
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

    private TermCell[] BlankFilledRow()
    {
        var row = new TermCell[_cols];
        for (int i = 0; i < _cols; i++)
        {
            row[i] = new TermCell { Ch = ' ', Fg = _fg, Bg = _bg };
        }
        return row;
    }

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
