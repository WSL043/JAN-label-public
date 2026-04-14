const corePillars = [
  "印刷コアは Rust で隔離する",
  "UI はジョブ作成と監査に集中する",
  "プリンタ差異は adapter 境界に閉じ込める"
];

export function App() {
  return (
    <main className="page">
      <section className="hero">
        <p className="eyebrow">Label Platform</p>
        <h1>Print Core First</h1>
        <p className="lede">
          Windows で寸法再現性を固め、同じ印刷コアを macOS / Linux / mobile から再利用する。
        </p>
      </section>

      <section className="panel">
        <h2>Initial Scope</h2>
        <ul>
          {corePillars.map((pillar) => (
            <li key={pillar}>{pillar}</li>
          ))}
        </ul>
      </section>

      <section className="grid">
        <article className="card">
          <span>Core</span>
          <strong>Rust crates</strong>
          <p>JAN 検証、テンプレート管理、レンダリング、監査ログ。</p>
        </article>
        <article className="card">
          <span>Ops</span>
          <strong>GitHub Guardrails</strong>
          <p>ゴールデンテスト、fixture validation、docs-guard を main で必須化。</p>
        </article>
        <article className="card">
          <span>Shell</span>
          <strong>Tauri Optional</strong>
          <p>Windows 配布が必要になった時だけ追加し、製品の中心には置かない。</p>
        </article>
      </section>
    </main>
  );
}

