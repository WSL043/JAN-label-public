# pdf-proof-baseline

初回 `v0.1.0` release 向けの printer-matrix 記録。  
これは物理プリンタ実測ではなく、`render` の PDF proof fixture を 100% スケール基準として確認した baseline です。

## Metadata

- measured_at: 2026-04-15
- operator: Codex
- printer model: PDF proof baseline
- driver version: N/A
- connection: local fixture inspection
- label stock: N/A (digital proof)
- template id / version: basic-label / v0.1.0
- printer profile id: pdf-proof/default

## Requested Output

- requested size: 50.0 mm x 30.0 mm
- output route: PDF proof
- dpi: vector
- scale policy: 100%

## Measured Result

- measured width: 50.0 mm
- measured height: 30.0 mm
- barcode scan result: not executed; PDF proof only
- notes: `packages/fixtures/golden/basic-label.pdf` の `/MediaBox [0 0 141.732 85.039]` を基準に換算。初回 release gate の baseline として扱い、物理プリンタ実測は別途追記する。

## Evidence

- job id: JOB-20260414-0001
- commit or tag: origin/main@26d1a12
- PDF proof path or artifact: `packages/fixtures/golden/basic-label.pdf`
- photo or screenshot: not captured
