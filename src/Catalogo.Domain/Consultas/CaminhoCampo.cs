using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Consultas;

/// <summary>
/// Um campo alcançado a partir de uma entidade: dela mesma ou, por até <see cref="MaximoSaltos"/> campos REFERENCIA, de um registro
/// APONTADO (RN89). É o ÚNICO intérprete de caminhos: os filtros da listagem (<see cref="InterpretadorConsulta"/>) e as dimensões e
/// métricas do painel (<see cref="Painel.InterpretadorIndicadores"/>) usam este mesmo <see cref="Resolver"/>. Em cenario_teste:
///
///   "numero_cenario"                  → o campo da própria entidade (nenhum salto);
///   "relatorio_teste.ambiente"        → o ambiente do relatório do cenário (1 salto);
///   "relatorio_teste.projeto.sigla"   → a sigla do projeto do relatório do cenário (2 saltos).
///
/// Cada registro aponta para no máximo um registro por campo REFERENCIA: um caminho dá no máximo UM valor por registro.
/// </summary>
/// <param name="Saltos">Os campos REFERENCIA atravessados, na ordem (o primeiro é da entidade consultada). Vazio = campo direto.</param>
/// <param name="Campo">O campo do fim do caminho (da entidade apontada pelo último salto).</param>
public sealed record CaminhoCampo(IReadOnlyList<Campo> Saltos, Campo Campo)
{
    public const int MaximoSaltos = 2;

    /// <summary>Como o caminho é escrito na URL: "relatorio_teste.ambiente".</summary>
    public string Chave => string.Join('.', Saltos.Select(s => s.Chave).Append(Campo.Chave));

    /// <summary>Para legendas e cabeçalhos: "Ambiente (Relatório)", "Sigla (Relatório › Projeto)"; sem saltos, o nome do campo.</summary>
    public string Nome => Saltos.Count == 0 ? Campo.Nome : $"{Campo.Nome} ({string.Join(" › ", Saltos.Select(s => s.Nome))})";

    /// <summary>
    /// Confere o caminho contra os metadados: cada salto é um campo REFERENCIA ATIVO cuja entidade apontada está no
    /// <paramref name="grupo"/>, e o último segmento é um campo ativo da entidade a que se chegou. O TIPO do último campo é
    /// conferido por quem chama (filtro, dimensão ou métrica têm regras diferentes).
    /// </summary>
    /// <param name="grupo">De onde vêm as entidades apontadas. Nulo = só campos diretos.</param>
    /// <param name="erro">A mensagem, se o caminho não vale (e o retorno é nulo).</param>
    public static CaminhoCampo? Resolver(Grupo? grupo, Entidade entidade, string caminho, out string? erro)
    {
        erro = null;
        var partes = caminho.Split('.');
        if (partes.Length - 1 > MaximoSaltos)
        {
            erro = $"'{caminho}': no máximo {MaximoSaltos} saltos por referência, ex.: relatorio_teste.projeto.sigla.";
            return null;
        }

        var dono = entidade;
        var saltos = new List<Campo>();
        for (var i = 0; i < partes.Length; i++)
        {
            var chave = partes[i];
            var percorrido = string.Join('.', partes[..(i + 1)]);
            if (chave.Length == 0)
            {
                erro = $"'{caminho}': caminho com um trecho vazio. Separe os campos com um ponto só, ex.: relatorio_teste.ambiente.";
                return null;
            }

            var campo = dono.ObterCampo(chave);
            if (campo is null)
            {
                erro = $"'{percorrido}': campo desconhecido em '{dono.Nome}'.";
                return null;
            }

            if (!campo.Ativo)
            {
                erro = $"'{percorrido}': campo inativo em '{dono.Nome}'.";
                return null;
            }

            if (i == partes.Length - 1)
                return new CaminhoCampo(saltos, campo);

            if (!campo.EhReferencia)
            {
                erro = $"'{caminho}': '{percorrido}' não é REFERENCIA; só se chega a campos de outra entidade por um campo REFERENCIA.";
                return null;
            }

            if (campo.EntidadeReferenciadaId is not Guid apontadaId || grupo?.ObterEntidade(apontadaId) is not { } apontada)
            {
                erro = $"'{caminho}': a entidade apontada por '{percorrido}' não está disponível.";
                return null;
            }

            saltos.Add(campo);
            dono = apontada;
        }

        // Inalcançável: Split devolve ao menos um trecho, e o último sempre retorna acima.
        throw new InvalidOperationException("Caminho sem campo.");
    }
}
