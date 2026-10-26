import { Registro } from '../../../core/registros/modelo-registro';
import { mensagemDeSalvo } from './pagina-formulario.component';

describe('mensagemDeSalvo (RN34, RN35)', () => {
  const registro = (situacaoAprovacao: string): Registro => ({ id: 'r1', dados: {}, situacaoAprovacao });

  it('criação comum e criação que vai para a aprovação', () => {
    expect(mensagemDeSalvo(null, registro('APROVADO'))).toBe('Item criado.');
    expect(mensagemDeSalvo(null, registro('PENDENTE'))).toBe('Item criado e enviado para aprovação.');
  });

  it('reenvio de um item rejeitado e edição de um pendente', () => {
    expect(mensagemDeSalvo(registro('REJEITADO'), registro('PENDENTE'))).toBe('Item reenviado para aprovação.');
    expect(mensagemDeSalvo(registro('PENDENTE'), registro('PENDENTE'))).toBe('Alterações salvas.');
    expect(mensagemDeSalvo(registro('APROVADO'), registro('APROVADO'))).toBe('Alterações salvas.');
  });
});
