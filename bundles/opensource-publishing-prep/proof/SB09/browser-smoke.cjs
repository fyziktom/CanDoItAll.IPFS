const fs = require("node:fs");
const path = require("node:path");
const { chromium } = require("playwright");

const baseUrl = process.env.SB09_BASE_URL || "http://127.0.0.1:5093";
const proofRoot = path.resolve(__dirname);
const screenshotDir = path.join(proofRoot, "screenshots");
const summaryPath = path.join(proofRoot, "browser-smoke-summary.json");

fs.mkdirSync(screenshotDir, { recursive: true });

const viewports = [
  { name: "1920x1080", width: 1920, height: 1080 },
  { name: "1600x900", width: 1600, height: 900 }
];

const routes = [
  { name: "files", path: "/files", waitMs: 7000 },
  { name: "content", path: "/content", waitMs: 4000 },
  { name: "network", path: "/network", waitMs: 4000 },
  { name: "settings", path: "/settings", waitMs: 4000 }
];

const summary = {
  baseUrl,
  startedAtUtc: new Date().toISOString(),
  screenshots: [],
  consoleErrors: [],
  pageErrors: [],
  failedRequests: [],
  modalResults: []
};

function wirePageDiagnostics(page, label) {
  page.on("console", message => {
    if (message.type() === "error") {
      summary.consoleErrors.push({
        label,
        text: message.text()
      });
    }
  });

  page.on("pageerror", error => {
    summary.pageErrors.push({
      label,
      message: error.message,
      stack: error.stack
    });
  });

  page.on("requestfailed", request => {
    const resourceType = request.resourceType();
    const failureText = request.failure()?.errorText ?? "unknown";
    const requestUrl = request.url();
    if (resourceType === "websocket"
      || resourceType === "eventsource"
      || resourceType === "ping"
      || (failureText === "net::ERR_ABORTED" && requestUrl.includes("/api/files/upload-browser"))) {
      return;
    }

    summary.failedRequests.push({
      label,
      url: requestUrl,
      method: request.method(),
      resourceType,
      failure: failureText
    });
  });
}

async function captureRoute(context, viewport, route) {
  const label = `${route.name}-${viewport.name}`;
  const page = await context.newPage();
  wirePageDiagnostics(page, label);

  await page.goto(`${baseUrl}${route.path}`, { waitUntil: "domcontentloaded", timeout: 30000 });
  await page.waitForTimeout(route.waitMs);

  const screenshotPath = path.join(screenshotDir, `SB09-${route.name}-${viewport.name}.png`);
  await page.screenshot({ path: screenshotPath, fullPage: false });
  summary.screenshots.push({
    label,
    route: route.path,
    viewport: viewport.name,
    path: screenshotPath
  });

  await page.close();
}

async function captureShareModal(context, viewport) {
  const label = `remote-pin-share-modal-${viewport.name}`;
  const page = await context.newPage();
  wirePageDiagnostics(page, label);

  await page.goto(`${baseUrl}/files`, { waitUntil: "domcontentloaded", timeout: 30000 });
  await page.waitForTimeout(7000);
  await seedPinnedFile(page);
  await page.goto(`${baseUrl}/files`, { waitUntil: "domcontentloaded", timeout: 30000 });
  await page.waitForTimeout(7000);

  await openShareAction(page);

  await page.getByText("Choose a receiver").waitFor({ timeout: 10000 });
  const screenshotPath = path.join(screenshotDir, `SB09-remote-pin-share-modal-${viewport.name}.png`);
  await page.screenshot({ path: screenshotPath, fullPage: false });
  summary.screenshots.push({
    label,
    route: "/files",
    viewport: viewport.name,
    path: screenshotPath
  });
  summary.modalResults.push({
    label,
    status: "opened"
  });

  await page.close();
}

async function seedPinnedFile(page) {
  await page.evaluate(async () => {
    const form = new FormData();
    form.append(
      "files",
      new File(["SB09 remote pin share modal seed"], "SB09-share-modal-seed.txt", { type: "text/plain" }));
    const response = await fetch("/api/files/upload-browser?pin=true&wrap=false", {
      method: "POST",
      body: form
    });
    if (!response.ok) {
      throw new Error(`Seed upload failed with HTTP ${response.status}`);
    }
  });
}

async function openShareAction(page) {
  for (let depth = 0; depth < 5; depth++) {
    const firstCard = page.locator("button.fx-card-button").first();
    await firstCard.waitFor({ timeout: 15000 });
    await firstCard.click({ button: "right" });

    const shareButton = page.locator(".fx-context-menu button", { hasText: "Share" }).first();
    try {
      await shareButton.waitFor({ timeout: 1500 });
      await shareButton.click();
      return;
    } catch {
      await page.keyboard.press("Escape");
      await firstCard.dblclick();
      await page.waitForTimeout(1500);
    }
  }

  throw new Error("Unable to find a shareable file card after drilling through Files virtual folders.");
}

(async () => {
  const browser = await chromium.launch();
  try {
    for (const viewport of viewports) {
      const context = await browser.newContext({
        viewport: { width: viewport.width, height: viewport.height }
      });

      for (const route of routes) {
        await captureRoute(context, viewport, route);
      }

      await captureShareModal(context, viewport);
      await context.close();
    }
  } finally {
    await browser.close();
  }

  summary.finishedAtUtc = new Date().toISOString();
  fs.writeFileSync(summaryPath, `${JSON.stringify(summary, null, 2)}\n`);

  if (summary.consoleErrors.length > 0 || summary.pageErrors.length > 0 || summary.failedRequests.length > 0) {
    process.exitCode = 1;
  }
})().catch(error => {
  summary.fatalError = {
    message: error.message,
    stack: error.stack
  };
  summary.finishedAtUtc = new Date().toISOString();
  fs.writeFileSync(summaryPath, `${JSON.stringify(summary, null, 2)}\n`);
  process.exitCode = 1;
});
