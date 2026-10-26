// Gera o cliente da API (models e services) em src/app/api/ a partir da cópia do contrato do back-end
// (openapi/v1.json, copiada de backend/openapi/v1.json do repositório do back-end), com o ng-openapi-gen.
// Rode com: npm run gerar-api
// NUNCA edite src/app/api/ à mão: tudo lá é apagado e recriado por este script.
import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';

const contrato = 'openapi/v1.json';
if (!existsSync(contrato)) {
  console.error(`Contrato não encontrado em ${contrato}. Rode este script da raiz do front-end.`);
  process.exit(1);
}

execFileSync('npx', ['ng-openapi-gen', '--config', 'ng-openapi-gen.json'], { stdio: 'inherit' });
console.log('Cliente da API gerado em src/app/api/.');
