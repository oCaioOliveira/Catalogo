#!/usr/bin/env python3
"""Reconfigura a área testes_desempenho PELA API de administração de metadados (as mesmas validações da tela), para a migração
dos relatórios do SharePoint (docs/migracao-sharepoint.md, seção "Estrutura da área").

Uso (API local no ar, área sem itens nos campos que mudam de tipo):
    CATALOGO_ADMIN_SENHA=... python3 database/migracao/reconfigurar_area_pela_api.py [--api http://localhost:5080]

As mudanças de tipo não são permitidas pela RN66: os 4 campos que mudam de tipo (bateria_testes, tipo_teste,
tempo_medio_resposta_ms, percentil_95_ms) são EXCLUÍDOS e RECRIADOS com a mesma chave (RN70: só sem valores), e a unicidade do
cenário é removida e criada de novo com o endpoint. Esses ganham GUIDs novos; todo o resto mantém os GUIDs.
Idempotente: o que já estiver como o desejado não é alterado. Para no primeiro erro.
"""
import argparse
import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

GRUPO = 'testes_desempenho'

parser = argparse.ArgumentParser()
parser.add_argument('--api', default=os.environ.get('CATALOGO_API', 'http://localhost:5080'))
parser.add_argument('--matricula', default=os.environ.get('CATALOGO_ADMIN_MATRICULA', 'admin'))
args = parser.parse_args()
API = args.api.rstrip('/') + '/api/v1'
SENHA = os.environ.get('CATALOGO_ADMIN_SENHA')
if not SENHA:
    sys.exit('Defina CATALOGO_ADMIN_SENHA.')


def chamar(metodo, caminho, corpo=None, token=None):
    req = urllib.request.Request(
        API + caminho, method=metodo, data=json.dumps(corpo).encode() if corpo is not None else None,
        headers={'Content-Type': 'application/json', **({'Authorization': 'Bearer ' + token} if token else {})})
    try:
        with urllib.request.urlopen(req) as r:
            texto = r.read()
            return r.status, (json.loads(texto) if texto else None)
    except urllib.error.HTTPError as e:
        texto = e.read()
        return e.code, (json.loads(texto) if texto else None)


status, corpo = chamar('POST', '/auth/login', {'matricula': args.matricula, 'senha': SENHA})
if status != 200:
    sys.exit(f'login: {status}')
TOKEN = corpo['token']
BASE = f'/admin/metadados/grupos/{GRUPO}'
grupo = None


def ler():
    global grupo
    s, g = chamar('GET', BASE, token=TOKEN)
    if s != 200:
        sys.exit(f'GET {BASE}: {s} {g}')
    grupo = g


def entidade(chave):
    return next(e for e in grupo['entidades'] if e['chave'] == chave)


def campo(ent, chave):
    return next((c for c in entidade(ent)['campos'] if c['chave'] == chave), None)


def escrever(metodo, caminho, corpo, descricao):
    s, r = chamar(metodo, caminho, corpo, TOKEN)
    if s not in (200, 201) or (r and r.get('permitido') is False):
        sys.exit(f'ERRO em "{descricao}": {s} {json.dumps(r, ensure_ascii=False)[:2000]}')
    print(f'  ok: {descricao}')
    ler()


def versao(ent):
    return entidade(ent)['versao']


def q(v):
    return urllib.parse.quote(v, safe='')


ATRIBUTOS = ('nome', 'tipoDado', 'obrigatorio', 'tamanhoMaximo', 'precisao', 'escala', 'opcoes')


def garantir_campo(ent, chave, desejado, recriar_se_tipo_diferente=False):
    """Cria o campo, ou altera os atributos dele. Tipo diferente: exclui e recria (só com recriar_se_tipo_diferente)."""
    atual = campo(ent, chave)
    corpo = {'chave': chave, 'obrigatorio': False, **desejado}
    if atual and atual['tipoDado'] != desejado['tipoDado']:
        if not recriar_se_tipo_diferente:
            sys.exit(f'{ent}.{chave}: tipo {atual["tipoDado"]} diferente de {desejado["tipoDado"]}')
        escrever('DELETE', f'{BASE}/entidades/{ent}/campos/{chave}?versao={q(versao(ent))}', None,
                 f'{ent}.{chave}: excluído para mudar o tipo ({atual["tipoDado"]} → {desejado["tipoDado"]})')
        atual = None
    if atual is None:
        escrever('POST', f'{BASE}/entidades/{ent}/campos', {'versao': versao(ent), **corpo}, f'{ent}.{chave}: criado')
        return
    if all(atual.get(a) == corpo.get(a) for a in ATRIBUTOS if a in corpo):
        print(f'  já ok: {ent}.{chave}')
        return
    escrever('PUT', f'{BASE}/entidades/{ent}/campos/{chave}', {'versao': versao(ent), **corpo}, f'{ent}.{chave}: alterado')


def garantir_ativo(ent, chave, ativo):
    if campo(ent, chave)['ativo'] != ativo:
        escrever('PATCH', f'{BASE}/entidades/{ent}/campos/{chave}/ativacao', {'versao': versao(ent), 'ativo': ativo},
                 f'{ent}.{chave}: ativo={ativo}')


def garantir_exibicao(ent, chave):
    e = entidade(ent)
    if e['campoExibicao'] == chave:
        return
    corpo = {'versao': e['versao'], 'nome': e['nome'], 'nomePlural': e['nomePlural'], 'descricao': e['descricao'],
             'ordem': e['ordem'], 'escopoEquipe': e['escopoEquipe'], 'campoExibicao': chave, 'icone': e['icone']}
    escrever('PUT', f'{BASE}/entidades/{ent}', corpo, f'{ent}: campo de exibição = {chave}')


def garantir_ordem(ent, chaves):
    atuais = [c['chave'] for c in sorted(entidade(ent)['campos'], key=lambda c: c['ordem'])]
    if set(atuais) != set(chaves):
        sys.exit(f'{ent}: ordem pedida não tem os mesmos campos: {sorted(set(atuais) ^ set(chaves))}')
    if atuais != chaves:
        escrever('PUT', f'{BASE}/entidades/{ent}/campos/ordem', {'versao': versao(ent), 'campos': chaves}, f'{ent}: ordem dos campos')


def garantir_restricao(ent, nome, campos, remover=()):
    for antiga in remover:
        if any(r['nome'] == antiga for r in entidade(ent)['restricoesUnicas']):
            escrever('DELETE', f'{BASE}/entidades/{ent}/restricoes/{antiga}?versao={q(versao(ent))}', None,
                     f'{ent}: unicidade {antiga} removida')
    atual = next((r for r in entidade(ent)['restricoesUnicas'] if r['nome'] == nome), None)
    if atual and sorted(atual['campos']) == sorted(campos):
        print(f'  já ok: {ent}.{nome}')
        return
    if atual:
        sys.exit(f'{ent}.{nome}: existe com outros campos {atual["campos"]}')
    escrever('POST', f'{BASE}/entidades/{ent}/restricoes', {'versao': versao(ent), 'nome': nome, 'campos': campos},
             f'{ent}: unicidade {nome} {campos}')


ler()
print('Relatório de Teste')
R = 'relatorio_teste'
garantir_campo(R, 'titulo', {'nome': 'Título', 'tipoDado': 'TEXTO', 'tamanhoMaximo': 200, 'obrigatorio': True})
garantir_exibicao(R, 'titulo')
garantir_campo(R, 'bateria_testes', {'nome': 'Nº da bateria', 'tipoDado': 'INTEIRO', 'obrigatorio': True}, recriar_se_tipo_diferente=True)
garantir_campo(R, 'tipo_teste', {'nome': 'Tecnologia', 'tipoDado': 'LISTA', 'opcoes': ['API', 'WEB', 'Fila MQ'], 'obrigatorio': True},
               recriar_se_tipo_diferente=True)
garantir_campo(R, 'rtc', {'nome': 'RTC', 'tipoDado': 'REFERENCIA', 'obrigatorio': False})
garantir_campo(R, 'comunidade_na_execucao', {'nome': 'Comunidade na execução', 'tipoDado': 'TEXTO', 'tamanhoMaximo': 100})
garantir_campo(R, 'host_gerenciador_filas', {'nome': 'Host / gerenciador de filas', 'tipoDado': 'TEXTO', 'tamanhoMaximo': 255})
garantir_campo(R, 'endpoints_testados', {'nome': 'Endpoints / filas testados', 'tipoDado': 'TEXTO_LONGO'})
garantir_campo(R, 'script', {'nome': 'Script', 'tipoDado': 'URL'})
garantir_campo(R, 'configuracoes_ambiente', {'nome': 'Configurações do ambiente', 'tipoDado': 'TEXTO_LONGO'})
garantir_campo(R, 'infraestrutura', {'nome': 'Infraestrutura', 'tipoDado': 'TEXTO', 'tamanhoMaximo': 500})
garantir_campo(R, 'observacoes_cenarios', {'nome': 'Observações dos cenários', 'tipoDado': 'TEXTO_LONGO'})
garantir_campo(R, 'dados_originais', {'nome': 'Dados originais (SharePoint)', 'tipoDado': 'TEXTO_LONGO'})
garantir_ativo(R, 'dados_originais', False)
garantir_ordem(R, ['titulo', 'projeto', 'rtc', 'ambiente', 'status_resultado', 'data_execucao', 'bateria_testes', 'tipo_teste',
                   'executado_por', 'comunidade_na_execucao', 'host_gerenciador_filas', 'endpoints_testados', 'contexto', 'objetivo',
                   'apontamentos', 'observacoes_cenarios', 'conclusao', 'script', 'configuracoes_ambiente', 'infraestrutura',
                   'dados_originais'])

print('Cenário de Teste')
C = 'cenario_teste'
garantir_campo(C, 'tempo_medio_resposta_ms', {'nome': 'Tempo médio (ms)', 'tipoDado': 'DECIMAL', 'precisao': 18, 'escala': 2,
                                              'obrigatorio': True}, recriar_se_tipo_diferente=True)
garantir_campo(C, 'percentil_95_ms', {'nome': 'Percentil 95 (ms)', 'tipoDado': 'DECIMAL', 'precisao': 18, 'escala': 2},
               recriar_se_tipo_diferente=True)
garantir_campo(C, 'descricao_cenario', {'nome': 'Descrição do cenário', 'tipoDado': 'TEXTO', 'tamanhoMaximo': 150})
garantir_campo(C, 'amostras', {'nome': 'Amostras', 'tipoDado': 'INTEIRO'})
garantir_restricao(C, 'uq_cenario_relatorio_numero_endpoint', ['relatorio_teste', 'numero_cenario', 'endpoint_servico'],
                   remover=['uq_cenario_relatorio_numero'])
garantir_ordem(C, ['relatorio_teste', 'endpoint_servico', 'numero_cenario', 'descricao_cenario', 'usuarios_virtuais',
                   'tempo_medio_resposta_ms', 'percentil_95_ms', 'vazao_media_tps', 'taxa_erro_percentual', 'amostras'])

print('Endpoint de Serviço')
E = 'endpoint_servico'
garantir_campo(E, 'nome_servico', {'nome': 'Nome do serviço', 'tipoDado': 'TEXTO', 'tamanhoMaximo': 500, 'obrigatorio': True})
garantir_campo(E, 'metodo', {'nome': 'Método', 'tipoDado': 'TEXTO', 'tamanhoMaximo': 50})
garantir_campo(E, 'endpoint_url_fila', {'nome': 'Endpoint / URL da fila', 'tipoDado': 'TEXTO', 'tamanhoMaximo': 500, 'obrigatorio': True})

print('RTC')
garantir_campo('rtc', 'url', {'nome': 'URL', 'tipoDado': 'URL'})
print('Concluído.')
