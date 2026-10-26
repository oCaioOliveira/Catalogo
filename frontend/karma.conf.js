// Karma (executor padrão de testes unitários do Angular 20). Roda no Chromium do Playwright, em modo
// headless, para não depender de um Chrome instalado na máquina: `npx playwright install chromium` uma vez.
const { chromium } = require('@playwright/test');

process.env.CHROME_BIN ??= chromium.executablePath();

module.exports = function (config) {
  config.set({
    basePath: '',
    frameworks: ['jasmine'],
    plugins: [
      require('karma-jasmine'),
      require('karma-chrome-launcher'),
      require('karma-jasmine-html-reporter'),
      require('karma-coverage'),
    ],
    client: { jasmine: { random: true }, clearContext: false },
    jasmineHtmlReporter: { suppressAll: true },
    coverageReporter: { dir: require('path').join(__dirname, './coverage'), subdir: '.', reporters: [{ type: 'text-summary' }] },
    reporters: ['progress', 'kjhtml'],
    browsers: ['ChromeHeadlessCatalogo'],
    customLaunchers: {
      ChromeHeadlessCatalogo: { base: 'ChromeHeadless', flags: ['--no-sandbox'] },
    },
    restartOnFileChange: true,
    // No WSL, a primeira execução depois de compilar às vezes passou dos 30 s padrão sem o navegador dar sinal ("Chrome Headless
    // Disconnected, because no message in 30000 ms"): o bundle novo demora a carregar. 120 s evitam esse falso erro.
    browserNoActivityTimeout: 120_000,
    captureTimeout: 120_000,
  });
};
