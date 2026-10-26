// Gera o cliente da API (models e services) em src/app/api/ a partir do contrato do back-end
// (../backend/openapi/v1.json), com o ng-openapi-gen. Rode com: npm run gerar-api
// NUNCA edite src/app/api/ à mão: tudo lá é apagado e recriado por este script.
import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';

const contrato = '../backend/openapi/v1.json';
if (!existsSync(contrato)) {
  console.error(`Contrato não encontrado em ${contrato}. Rode este script de dentro de frontend/.`);
  process.exit(1);
}

execFileSync('npx', ['ng-openapi-gen', '--config', 'ng-openapi-gen.json'], { stdio: 'inherit' });
console.log('Cliente da API gerado em src/app/api/.');
