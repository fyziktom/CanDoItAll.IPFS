# Playwright MCP Browser Proof Summary

## Viewport

- Large desktop viewport: `1600x1000`.

## IPFS NodeControl

- Before route: `http://127.0.0.1:5093/`
- Before screenshot: `bundle://proof/SB02/browser/ipfs-before.png`
- Before CSS assertion: BaseLib `/_content/CanDoItAll.Components.BaseLib/css/output.css` returned `200`, `text/css`, length `174525`, not HTML.
- After route: `http://127.0.0.1:5093/`
- After screenshot: `bundle://proof/SB02/browser/ipfs-after.png`
- After CSS assertion: BaseLib `output.css` returned `200`, `text/css`, length `174525`, not HTML; `.cda-button` computed `display=flex`, `border-radius=14px`; dashboard content contained `Node identity`.
- Visual review: BaseLib shell, navigation icons, pills, buttons, tabs, section cards, and route content remained styled. Pixel diff after recapture was `0.1564%`, concentrated in dynamic node summary values.

## Economy Components Demo

- Before route: `http://localhost:56426/`
- Before screenshot: `bundle://proof/SB02/browser/economy-components-demo-before.png`
- Before CSS assertion: BaseLib `output.css` returned `200`, `text/css`, length `174525`, not HTML.
- After route: `http://localhost:56426/`
- After screenshot: `bundle://proof/SB02/browser/economy-components-demo-after.png`
- After CSS assertion: BaseLib `output.css` returned `200`, `text/css`, length `174525`, not HTML.
- Visual review: sidebar, action buttons, tables, recent transaction panels, and charts remained styled. Pixel diff was `0.5859%`, matching small seeded-data/runtime text differences and no layout collapse.

## Economy Simulator App

- Before route: `http://localhost:51139/`
- Before screenshot: `bundle://proof/SB02/browser/economy-simulator-before.png`
- Before CSS assertion: BaseLib `output.css` returned `200`, `text/css`, length `174525`, not HTML.
- After route: `http://localhost:51139/`
- After screenshot: `bundle://proof/SB02/browser/economy-simulator-after.png`
- After CSS assertion: BaseLib `output.css` returned `200`, `text/css`, length `174525`, not HTML; `.cda-button` computed `display=flex`, `border-radius=14px`.
- Visual review: project rail, action buttons, tabs, status badges, forms, and validation panel remained styled. Pixel diff was `0.0023%`, limited to timestamp text.

## Screenshot Diff Transcript

- `bundle://proof/SB02/commands/screenshot-diff-summary.txt`
