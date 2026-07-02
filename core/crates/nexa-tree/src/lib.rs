//! nexa-tree — 인라인 트리 + 교차 선택 모델(가시 노드 평면 스트림, C1).
//!
//! 트리를 **가시 노드의 평면 스트림**(`VisibleRow`)으로 투영해 가상화 렌더 + 빠른 선택을
//! 동시에 달성한다(설계: docs/07 · ADR-0004 docs/29). UI 비종속 순수 로직 → 맥 단위테스트.
//!
//! 슬라이스 1(이 크레이트): open/expand/collapse/가시행/선택(OrderedSet). ABI·앱은 후속 슬라이스.

use std::collections::HashSet;
use std::io;
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};

use nexa_core::FileKind;
use nexa_vfs::read_dir_entries;

/// 트리 세션 내 안정 식별자(삽입 순번, 회수하지 않음 → arena 인덱스와 동일).
pub type NodeId = u64;

/// 트리 노드(arena 저장). `children`은 `loaded == true`일 때만 유효(정렬된 순서).
#[derive(Debug, Clone)]
struct Node {
    id: NodeId,
    /// 부모(최상위는 `None`). 슬라이스 2/3(ABI·경로변동 추적)에서 사용.
    #[allow(dead_code)]
    parent: Option<NodeId>,
    path: PathBuf,
    name: String,
    kind: FileKind,
    depth: u32,
    size: u64,
    modified_unix_ms: i64,
    attrs: u32,
    expanded: bool,
    loaded: bool,
    children: Vec<NodeId>,
}

impl Node {
    fn is_dir(&self) -> bool {
        self.kind == FileKind::Dir
    }
}

/// UI로 흘려보내는 가시 행 단위(코어→호스트).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct VisibleRow {
    pub id: NodeId,
    pub depth: u32,
    pub kind: FileKind,
    pub name: String,
    pub size: u64,
    pub modified_unix_ms: i64,
    pub attrs: u32,
    pub expanded: bool,
    /// 펼칠 수 있는가(디렉터리). 심링크는 슬라이스 1에서 펼침 대상 아님.
    pub has_children: bool,
}

/// 펼침/접힘으로 인한 가시 목록 변경 구간(호스트가 행 삽입/삭제에 사용).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct RangeChange {
    pub start: usize,
    pub removed: usize,
    pub inserted: usize,
}

impl RangeChange {
    /// 변경 없음.
    pub const NONE: RangeChange = RangeChange {
        start: 0,
        removed: 0,
        inserted: 0,
    };
}

/// 선택 갱신 방식.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SelectMode {
    /// 단일 선택(기존 해제) + anchor 갱신.
    Single,
    /// 비연속 토글(다중) + anchor 갱신.
    Toggle,
}

/// Windows 숨김 속성 비트(FILE_ATTRIBUTE_HIDDEN).
const ATTR_HIDDEN: u32 = 0x2;

/// 가시성 필터(숨김 속성·점 파일). 앱 `ViewOptions`와 동일 개념(둘 다 "보기").
/// 열거 시 적용 — 걸러진 항목은 트리에 아예 생성하지 않는다.
#[derive(Debug, Clone, Copy)]
struct Filter {
    show_hidden: bool,
    show_dotfiles: bool,
}

impl Filter {
    fn allows(&self, name: &str, attrs: u32) -> bool {
        if !self.show_dotfiles && name.starts_with('.') {
            return false;
        }
        if !self.show_hidden && (attrs & ATTR_HIDDEN) != 0 {
            return false;
        }
        true
    }
}

/// 인라인 트리 + 선택 상태. 임의 부모의 노드를 함께 선택할 수 있다(교차 선택).
#[derive(Debug)]
pub struct Tree {
    nodes: Vec<Node>,
    roots: Vec<NodeId>,
    visible: Vec<NodeId>,
    sel_order: Vec<NodeId>, // OrderedSet: 삽입 순서 보존
    sel_set: HashSet<NodeId>,
    anchor: Option<NodeId>,
    root_path: PathBuf,
    filter: Filter,
}

impl Tree {
    /// `path`를 열어 최상위(depth 0) 항목을 열거한 트리를 만든다(펼침 없음, **모두 표시**).
    pub fn open(path: impl AsRef<Path>) -> io::Result<Tree> {
        Tree::open_filtered(path, true, true)
    }

    /// 가시성 필터를 적용해 연다. `show_hidden`=Windows 숨김 속성, `show_dotfiles`=점(.) 파일.
    /// 걸러진 항목은 트리에 생성되지 않으므로 펼침 시 자식도 동일 필터가 적용된다.
    pub fn open_filtered(
        path: impl AsRef<Path>,
        show_hidden: bool,
        show_dotfiles: bool,
    ) -> io::Result<Tree> {
        let root_path = path.as_ref().to_path_buf();
        let mut tree = Tree {
            nodes: Vec::new(),
            roots: Vec::new(),
            visible: Vec::new(),
            sel_order: Vec::new(),
            sel_set: HashSet::new(),
            anchor: None,
            root_path: root_path.clone(),
            filter: Filter {
                show_hidden,
                show_dotfiles,
            },
        };
        let roots = tree.enumerate(&root_path, None, 0)?;
        tree.roots.clone_from(&roots);
        tree.visible = roots;
        Ok(tree)
    }

    /// 열린 루트 경로.
    pub fn root_path(&self) -> &Path {
        &self.root_path
    }

    /// 최상위 항목 수(펼침과 무관).
    pub fn root_count(&self) -> usize {
        self.roots.len()
    }

    // ── 열거 ──────────────────────────────────────────────────

    /// `dir`의 자식을 열거해 arena에 추가하고 정렬된 id 목록을 반환한다.
    /// 엔트리 단위 오류는 격리(해당 항목만 건너뜀).
    fn enumerate(
        &mut self,
        dir: &Path,
        parent: Option<NodeId>,
        depth: u32,
    ) -> io::Result<Vec<NodeId>> {
        let mut ids = Vec::new();
        for entry in read_dir_entries(dir)? {
            let Ok(e) = entry else { continue };
            if !self.filter.allows(&e.name, e.attrs) {
                continue; // 숨김/점 파일 필터(트리에 아예 생성 안 함)
            }
            let id = self.nodes.len() as NodeId;
            let path = dir.join(&e.name);
            self.nodes.push(Node {
                id,
                parent,
                path,
                name: e.name,
                kind: e.kind,
                depth,
                size: e.size,
                modified_unix_ms: to_unix_ms(e.modified),
                attrs: e.attrs,
                expanded: false,
                loaded: false,
                children: Vec::new(),
            });
            ids.push(id);
        }
        self.sort_ids(&mut ids);
        Ok(ids)
    }

    /// 폴더 우선 + 이름 오름차순(대소문자 무시). 앱 `SortItems`와 동일 규약.
    fn sort_ids(&self, ids: &mut [NodeId]) {
        ids.sort_by(|&a, &b| {
            let na = &self.nodes[a as usize];
            let nb = &self.nodes[b as usize];
            nb.is_dir()
                .cmp(&na.is_dir())
                .then_with(|| na.name.to_lowercase().cmp(&nb.name.to_lowercase()))
        });
    }

    // ── 가시 스트림 ─────────────────────────────────────────────

    /// 현재 가시 행 수.
    pub fn visible_len(&self) -> usize {
        self.visible.len()
    }

    /// 가시 목록이 비었는가.
    pub fn is_empty(&self) -> bool {
        self.visible.is_empty()
    }

    /// 가시 인덱스의 행. 범위 밖이면 `None`.
    pub fn row(&self, index: usize) -> Option<VisibleRow> {
        let id = *self.visible.get(index)?;
        let n = &self.nodes[id as usize];
        Some(VisibleRow {
            id: n.id,
            depth: n.depth,
            kind: n.kind,
            name: n.name.clone(),
            size: n.size,
            modified_unix_ms: n.modified_unix_ms,
            attrs: n.attrs,
            expanded: n.expanded,
            has_children: n.is_dir(),
        })
    }

    /// 가시 목록에서 `id`의 인덱스(선형 탐색; 대규모 최적화는 슬라이스 4).
    fn visible_index(&self, id: NodeId) -> Option<usize> {
        self.visible.iter().position(|&x| x == id)
    }

    /// `id`의 펼침 하위(자식과 그 펼친 후손)를 가시 순서(DFS)로 `out`에 모은다.
    fn collect_subtree(&self, id: NodeId, out: &mut Vec<NodeId>) {
        for &c in &self.nodes[id as usize].children {
            out.push(c);
            let cn = &self.nodes[c as usize];
            if cn.is_dir() && cn.expanded {
                self.collect_subtree(c, out);
            }
        }
    }

    // ── 펼침 / 접힘 ─────────────────────────────────────────────

    /// `id`(디렉터리)를 펼친다. 최초면 지연 열거. 이미 펼쳤거나 디렉터리가 아니거나
    /// 가시 상태가 아니면 무변경(`RangeChange::NONE`). 이전에 접힌 하위의 펼침 상태는 복원한다.
    pub fn expand(&mut self, id: NodeId) -> io::Result<RangeChange> {
        match self.nodes.get(id as usize) {
            Some(n) if n.is_dir() && !n.expanded => {}
            _ => return Ok(RangeChange::NONE),
        }
        let Some(vis) = self.visible_index(id) else {
            return Ok(RangeChange::NONE);
        };
        if !self.nodes[id as usize].loaded {
            let path = self.nodes[id as usize].path.clone();
            let depth = self.nodes[id as usize].depth + 1;
            let children = self.enumerate(&path, Some(id), depth)?;
            self.nodes[id as usize].children = children;
            self.nodes[id as usize].loaded = true;
        }
        self.nodes[id as usize].expanded = true;

        let mut sub = Vec::new();
        self.collect_subtree(id, &mut sub);
        let start = vis + 1;
        let inserted = sub.len();
        self.visible.splice(start..start, sub);
        Ok(RangeChange {
            start,
            removed: 0,
            inserted,
        })
    }

    /// `id`를 접는다. 펼침 상태가 아니거나 가시 상태가 아니면 무변경. 하위의 펼침 상태는 보존.
    pub fn collapse(&mut self, id: NodeId) -> RangeChange {
        match self.nodes.get(id as usize) {
            Some(n) if n.expanded => {}
            _ => return RangeChange::NONE,
        }
        let Some(vis) = self.visible_index(id) else {
            return RangeChange::NONE;
        };
        let base_depth = self.nodes[id as usize].depth;
        self.nodes[id as usize].expanded = false;

        let start = vis + 1;
        let mut count = 0;
        while let Some(&nid) = self.visible.get(start + count) {
            if self.nodes[nid as usize].depth > base_depth {
                count += 1;
            } else {
                break;
            }
        }
        self.visible.drain(start..start + count);
        RangeChange {
            start,
            removed: count,
            inserted: 0,
        }
    }

    /// `id`의 펼침 여부(범위 밖이면 `None`).
    pub fn is_expanded(&self, id: NodeId) -> Option<bool> {
        self.nodes.get(id as usize).map(|n| n.expanded)
    }

    // ── 선택 (OrderedSet, 교차 폴더 허용) ────────────────────────

    /// 단일/토글 선택 + anchor 갱신.
    pub fn select(&mut self, id: NodeId, mode: SelectMode) {
        match mode {
            SelectMode::Single => {
                self.clear_selection();
                self.add_sel(id);
            }
            SelectMode::Toggle => {
                if self.sel_set.contains(&id) {
                    self.remove_sel(id);
                } else {
                    self.add_sel(id);
                }
            }
        }
        self.anchor = Some(id);
    }

    /// anchor~`id`의 가시 범위 선택(anchor 없으면 단일). anchor는 유지.
    pub fn select_range(&mut self, id: NodeId) {
        let Some(anchor) = self.anchor else {
            self.select(id, SelectMode::Single);
            return;
        };
        let (Some(ia), Some(ib)) = (self.visible_index(anchor), self.visible_index(id)) else {
            self.select(id, SelectMode::Single);
            return;
        };
        let (lo, hi) = if ia <= ib { (ia, ib) } else { (ib, ia) };
        self.clear_selection();
        for idx in lo..=hi {
            let nid = self.visible[idx];
            self.add_sel(nid);
        }
    }

    /// 현재 가시 노드 전체 선택.
    pub fn select_all_visible(&mut self) {
        self.clear_selection();
        for i in 0..self.visible.len() {
            let nid = self.visible[i];
            self.add_sel(nid);
        }
        self.anchor = self.visible.first().copied();
    }

    /// 선택 해제(anchor는 유지).
    pub fn clear_selection(&mut self) {
        self.sel_order.clear();
        self.sel_set.clear();
    }

    fn add_sel(&mut self, id: NodeId) {
        if self.sel_set.insert(id) {
            self.sel_order.push(id);
        }
    }

    fn remove_sel(&mut self, id: NodeId) {
        if self.sel_set.remove(&id) {
            if let Some(pos) = self.sel_order.iter().position(|&x| x == id) {
                self.sel_order.remove(pos);
            }
        }
    }

    /// `id`가 선택됐는가.
    pub fn is_selected(&self, id: NodeId) -> bool {
        self.sel_set.contains(&id)
    }

    /// 선택 노드 id(삽입 순서).
    pub fn selected_ids(&self) -> &[NodeId] {
        &self.sel_order
    }

    /// 선택 수.
    pub fn selection_count(&self) -> usize {
        self.sel_order.len()
    }

    /// 선택 노드의 경로(삽입 순서) — 작업 엔진 입력(혼합 부모 허용).
    pub fn selected_paths(&self) -> Vec<&Path> {
        self.sel_order
            .iter()
            .map(|&id| self.nodes[id as usize].path.as_path())
            .collect()
    }

    /// 현재 anchor.
    pub fn anchor(&self) -> Option<NodeId> {
        self.anchor
    }

    /// 노드 경로(범위 밖이면 `None`). ABI/작업 엔진용.
    pub fn node_path(&self, id: NodeId) -> Option<&Path> {
        self.nodes.get(id as usize).map(|n| n.path.as_path())
    }
}

/// `SystemTime` → Unix epoch 밀리초(없으면 -1). 인터롭 표기와 동일.
fn to_unix_ms(t: Option<SystemTime>) -> i64 {
    t.and_then(|t| t.duration_since(UNIX_EPOCH).ok())
        .map_or(-1, |d| d.as_millis() as i64)
}

#[cfg(test)]
impl Tree {
    /// 파일시스템 없이 합성 노드로 채운 트리(벤치/스케일 테스트 전용).
    /// 최상위 `dirs`개 폴더 × 각 `per_dir`개 파일 자식(모두 `loaded`, 접힘 상태).
    /// 최상위만 가시(dirs행). 실제 열거 비용을 제거하고 순수 트리 연산만 측정.
    fn synthetic(dirs: usize, per_dir: usize) -> Tree {
        let mut nodes: Vec<Node> = Vec::with_capacity(dirs * (per_dir + 1));
        let mut roots = Vec::with_capacity(dirs);
        for d in 0..dirs {
            let dir_id = nodes.len() as NodeId;
            let dir_name = format!("dir{d:05}");
            let dir_path = PathBuf::from(&dir_name);
            let mut children = Vec::with_capacity(per_dir);
            // 자식 먼저 예약할 수 없으니 부모 push 후 자식 push, children는 나중에 세팅.
            nodes.push(Node {
                id: dir_id,
                parent: None,
                path: dir_path.clone(),
                name: dir_name,
                kind: FileKind::Dir,
                depth: 0,
                size: 0,
                modified_unix_ms: -1,
                attrs: 0,
                expanded: false,
                loaded: true,
                children: Vec::new(),
            });
            for f in 0..per_dir {
                let cid = nodes.len() as NodeId;
                let cname = format!("f{f:05}.txt");
                nodes.push(Node {
                    id: cid,
                    parent: Some(dir_id),
                    path: dir_path.join(&cname),
                    name: cname,
                    kind: FileKind::File,
                    depth: 1,
                    size: 0,
                    modified_unix_ms: -1,
                    attrs: 0,
                    expanded: false,
                    loaded: true,
                    children: Vec::new(),
                });
                children.push(cid);
            }
            nodes[dir_id as usize].children = children;
            roots.push(dir_id);
        }
        Tree {
            nodes,
            visible: roots.clone(),
            roots,
            sel_order: Vec::new(),
            sel_set: HashSet::new(),
            anchor: None,
            root_path: PathBuf::from("<synthetic>"),
            filter: Filter {
                show_hidden: true,
                show_dotfiles: true,
            },
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;
    use std::time::Instant;

    /// AC5 벤치(NFR-P1/P2, docs/07·29) — 10만 가시 노드에서 트리 연산이 UI 프레임 예산 안인지.
    /// 타이밍 단언은 CI 머신 편차로 불안정하므로 하지 않고(`--ignored`로 수동 측정),
    /// 대신 **연산이 완료됨**(무한/이차 폭주 방지)만 확인한다. `-- --ignored --nocapture`로 수치 확인.
    #[test]
    #[ignore = "수동 벤치: cargo test -p nexa-tree -- --ignored --nocapture"]
    fn bench_100k_visible() {
        let dirs = 100;
        let per_dir = 1000; // 100 × 1000 = 100,000 파일 + 100 폴더
        let build = Instant::now();
        let mut t = Tree::synthetic(dirs, per_dir);
        eprintln!(
            "[bench] synthetic build: {:?} ({} nodes)",
            build.elapsed(),
            t.nodes.len()
        );

        // 전체 펼침 → 100,100 가시 행. 각 expand는 splice(꼬리 이동)를 포함.
        let expand = Instant::now();
        let root_ids: Vec<NodeId> = t.roots.clone();
        for id in &root_ids {
            t.expand(*id).unwrap();
        }
        let vis = t.visible_len();
        eprintln!(
            "[bench] expand {dirs} dirs → {vis} visible rows: {:?}",
            expand.elapsed()
        );
        assert_eq!(vis, dirs + dirs * per_dir);

        // 무작위 위치 10,000회 visible_index 조회(현재 O(n) 선형) — 병목 후보 측정.
        let lookups = 10_000usize;
        let probe = Instant::now();
        let mut acc = 0usize;
        for k in 0..lookups {
            let target = t.visible[(k * 7919) % vis]; // 흩뿌린 인덱스
            acc += t.visible_index(target).unwrap();
        }
        eprintln!(
            "[bench] {lookups}× visible_index: {:?} (acc={acc})",
            probe.elapsed()
        );

        // 행 조회 전체 순회(호스트 마샬 전 코어 비용).
        let rows = Instant::now();
        for i in 0..vis {
            let _ = t.row(i).unwrap();
        }
        eprintln!("[bench] row() × {vis}: {:?}", rows.elapsed());

        // 전체 선택 + 접힘.
        let sel = Instant::now();
        t.select_all_visible();
        eprintln!(
            "[bench] select_all_visible ({}): {:?}",
            t.selection_count(),
            sel.elapsed()
        );
        let col = Instant::now();
        for id in &root_ids {
            t.collapse(*id);
        }
        eprintln!(
            "[bench] collapse {dirs} dirs → {} visible: {:?}",
            t.visible_len(),
            col.elapsed()
        );
    }

    /// 스케일 가드(CI 상시) — 10만 노드에서 핵심 연산이 정상 완료(이차 폭주·패닉 없음).
    #[test]
    fn large_tree_scale_ops_complete() {
        let mut t = Tree::synthetic(20, 5000); // 20 × 5000 = 100,000 + 20
        for id in t.roots.clone() {
            t.expand(id).unwrap();
        }
        let vis = t.visible_len();
        assert_eq!(vis, 20 + 20 * 5000);
        // 경계 행 조회.
        assert!(t.row(0).unwrap().has_children);
        assert!(t.row(vis - 1).is_some());
        assert!(t.row(vis).is_none());
        // 위치 조회(끝 근처) + 선택.
        let last = t.visible[vis - 1];
        assert_eq!(t.visible_index(last), Some(vis - 1));
        t.select(last, SelectMode::Single);
        assert!(t.is_selected(last));
        // 첫 폴더 접기 → 5000 제거.
        let removed = t.collapse(t.roots[0]).removed;
        assert_eq!(removed, 5000);
        assert_eq!(t.visible_len(), vis - 5000);
    }

    /// 격리된 임시 트리: base/{dirA/{x.txt,y.txt}, dirA/dirB/z.txt, file1.txt}.
    fn make_fixture(tag: &str) -> PathBuf {
        let base = std::env::temp_dir().join(format!("nexa_tree_{}_{}", tag, std::process::id()));
        let _ = fs::remove_dir_all(&base);
        fs::create_dir_all(base.join("dirA/dirB")).unwrap();
        fs::write(base.join("dirA/x.txt"), b"x").unwrap();
        fs::write(base.join("dirA/y.txt"), b"yy").unwrap();
        fs::write(base.join("dirA/dirB/z.txt"), b"zzz").unwrap();
        fs::write(base.join("file1.txt"), b"f").unwrap();
        base
    }

    fn names(t: &Tree) -> Vec<String> {
        (0..t.visible_len())
            .map(|i| t.row(i).unwrap().name)
            .collect()
    }

    #[test]
    fn open_lists_top_level_folders_first() {
        let base = make_fixture("open");
        let t = Tree::open(&base).unwrap();
        fs::remove_dir_all(&base).unwrap();

        assert_eq!(t.visible_len(), 2);
        assert_eq!(names(&t), vec!["dirA", "file1.txt"]);
        assert!(t.row(0).unwrap().has_children); // dir
        assert!(!t.row(0).unwrap().expanded);
        assert!(!t.row(1).unwrap().has_children); // file
    }

    #[test]
    fn expand_and_collapse_roundtrip() {
        let base = make_fixture("expcol");
        let mut t = Tree::open(&base).unwrap();
        let dir_a = t.row(0).unwrap().id;

        let c = t.expand(dir_a).unwrap();
        // dirA 자식 3개(dirB·x.txt·y.txt)가 삽입됨
        assert_eq!(
            c,
            RangeChange {
                start: 1,
                removed: 0,
                inserted: 3
            }
        );
        // dirA / dirB / x.txt / y.txt / file1.txt  (dirB=폴더 우선)
        assert_eq!(
            names(&t),
            vec!["dirA", "dirB", "x.txt", "y.txt", "file1.txt"]
        );
        assert_eq!(t.row(1).unwrap().depth, 1);
        assert!(t.row(0).unwrap().expanded);

        let c2 = t.collapse(dir_a);
        assert_eq!(
            c2,
            RangeChange {
                start: 1,
                removed: 3,
                inserted: 0
            }
        );
        fs::remove_dir_all(&base).unwrap();
        assert_eq!(names(&t), vec!["dirA", "file1.txt"]);
    }

    #[test]
    fn reexpand_restores_nested_expansion() {
        let base = make_fixture("nested");
        let mut t = Tree::open(&base).unwrap();
        let dir_a = t.row(0).unwrap().id;
        t.expand(dir_a).unwrap();
        let dir_b = t.row(1).unwrap().id; // dirB
        assert_eq!(t.row(1).unwrap().name, "dirB");
        t.expand(dir_b).unwrap();
        assert_eq!(
            names(&t),
            vec!["dirA", "dirB", "z.txt", "x.txt", "y.txt", "file1.txt"]
        );

        t.collapse(dir_a);
        assert_eq!(names(&t), vec!["dirA", "file1.txt"]);
        // 재펼침 시 dirB의 펼침 상태(z.txt)가 복원돼야 함
        t.expand(dir_a).unwrap();
        fs::remove_dir_all(&base).unwrap();
        assert_eq!(
            names(&t),
            vec!["dirA", "dirB", "z.txt", "x.txt", "y.txt", "file1.txt"]
        );
    }

    #[test]
    fn expand_is_noop_on_file_or_twice() {
        let base = make_fixture("noop");
        let mut t = Tree::open(&base).unwrap();
        let file1 = t.row(1).unwrap().id;
        assert_eq!(t.expand(file1).unwrap(), RangeChange::NONE); // 파일

        let dir_a = t.row(0).unwrap().id;
        t.expand(dir_a).unwrap();
        assert_eq!(t.expand(dir_a).unwrap(), RangeChange::NONE); // 이미 펼침
        fs::remove_dir_all(&base).unwrap();
    }

    #[test]
    fn cross_folder_selection_ordered() {
        let base = make_fixture("sel");
        let mut t = Tree::open(&base).unwrap();
        let dir_a = t.row(0).unwrap().id;
        t.expand(dir_a).unwrap(); // dirA/dirB/x.txt/y.txt/file1.txt

        let x_id = t.row(2).unwrap().id; // x.txt (dirA 자식)
        let file1 = t.row(4).unwrap().id; // file1.txt (루트)
        assert_eq!(t.row(2).unwrap().name, "x.txt");
        assert_eq!(t.row(4).unwrap().name, "file1.txt");

        t.select(x_id, SelectMode::Single);
        t.select(file1, SelectMode::Toggle); // 서로 다른 부모 동시 선택
        assert!(t.is_selected(x_id) && t.is_selected(file1));
        assert_eq!(t.selected_ids(), &[x_id, file1]); // 삽입 순서
        assert_eq!(t.selection_count(), 2);

        t.select(file1, SelectMode::Toggle); // 토글 해제
        assert!(!t.is_selected(file1));
        assert_eq!(t.selected_ids(), &[x_id]);
        fs::remove_dir_all(&base).unwrap();
    }

    #[test]
    fn range_and_select_all() {
        let base = make_fixture("range");
        let mut t = Tree::open(&base).unwrap();
        let dir_a = t.row(0).unwrap().id;
        t.expand(dir_a).unwrap(); // 5 rows

        let first = t.row(1).unwrap().id;
        let fourth = t.row(3).unwrap().id;
        t.select(first, SelectMode::Single); // anchor=first(row1)
        t.select_range(fourth); // row1..row3
        assert_eq!(t.selection_count(), 3);
        assert!(t.is_selected(t.row(1).unwrap().id));
        assert!(t.is_selected(t.row(3).unwrap().id));
        assert!(!t.is_selected(t.row(4).unwrap().id));

        t.select_all_visible();
        assert_eq!(t.selection_count(), t.visible_len());
        t.clear_selection();
        assert_eq!(t.selection_count(), 0);
        fs::remove_dir_all(&base).unwrap();
    }

    #[test]
    fn open_filtered_excludes_dotfiles() {
        let base = std::env::temp_dir().join(format!("nexa_tree_filter_{}", std::process::id()));
        let _ = fs::remove_dir_all(&base);
        fs::create_dir_all(&base).unwrap();
        fs::write(base.join(".hidden"), b"h").unwrap();
        fs::write(base.join("visible.txt"), b"v").unwrap();

        let all = Tree::open(&base).unwrap();
        let no_dot = Tree::open_filtered(&base, true, false).unwrap();
        fs::remove_dir_all(&base).unwrap();

        assert_eq!(all.visible_len(), 2); // 기본 open = 모두 표시
        assert_eq!(no_dot.visible_len(), 1); // .hidden 제외
        assert_eq!(no_dot.row(0).unwrap().name, "visible.txt");
    }

    #[test]
    fn open_missing_path_errors() {
        let missing = std::env::temp_dir().join("nexa_tree_missing_zzz_does_not_exist");
        assert!(Tree::open(&missing).is_err());
    }
}
