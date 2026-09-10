"""Gera o PDF somente a partir de dados locais.
Uso: python docs/security-audit/gerar_relatorio.py
Dependências: reportlab>=4, pypdf>=5. Fontes Vera distribuídas em fontes/.
"""
import json, pathlib, textwrap, html, collections
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, PageBreak, Table, TableStyle, Preformatted, KeepTogether
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.graphics.shapes import Drawing, Circle, Rect, String
from reportlab.graphics.charts.piecharts import Pie
from pypdf import PdfReader
O=pathlib.Path(__file__).resolve().parent
D=json.loads((O/'dados.json').read_text(encoding='utf-8'))
for n,f in [('Vera','Vera.ttf'),('VeraBold','VeraBd.ttf')]: pdfmetrics.registerFont(TTFont(n,str(O/'fontes'/f)))
pdfmetrics.registerFontFamily('Vera',normal='Vera',bold='VeraBold',italic='Vera',boldItalic='VeraBold')
S=getSampleStyleSheet()
S.add(ParagraphStyle(name='Corpo',fontName='Vera',fontSize=9.5,leading=14,spaceAfter=8,textColor=colors.HexColor('#243247')))
S.add(ParagraphStyle(name='Titulo',fontName='VeraBold',fontSize=23,leading=30,spaceAfter=18,textColor=colors.HexColor('#123049')))
S.add(ParagraphStyle(name='Secao',fontName='VeraBold',fontSize=16,leading=21,spaceAfter=12,textColor=colors.HexColor('#123049')))
S.add(ParagraphStyle(name='Sub',fontName='VeraBold',fontSize=11,leading=16,spaceBefore=9,spaceAfter=7,textColor=colors.HexColor('#123049')))
S.add(ParagraphStyle(name='Pequeno',fontName='Vera',fontSize=7.5,leading=11,spaceAfter=5,wordWrap='CJK'))
S.add(ParagraphStyle(name='Codigo',fontName='Courier',fontSize=7.2,leading=10,spaceAfter=8))
COL={'crítica':'#B91C1C','alta':'#EA580C','média':'#D97706','baixa':'#2563EB','informativa':'#64748B'}
F=D['achados']; story=[]
def p(t,style='Corpo'): return Paragraph(html.escape(str(t)).replace('\n','<br/>'),S[style])
def add(t,style='Corpo'): story.append(p(t,style))
def titulo(t): add(t,'Secao')
def tab(rows,widths):
 t=Table([[p(c,'Pequeno') for c in row] for row in rows],colWidths=widths,repeatRows=1,hAlign='LEFT')
 t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#E5EDF2')),('VALIGN',(0,0),(-1,-1),'TOP'),('BOTTOMPADDING',(0,0),(-1,-1),7),('TOPPADDING',(0,0),(-1,-1),7),('LINEBELOW',(0,0),(-1,0),.7,colors.HexColor('#78909C')),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,colors.HexColor('#F7F9FB')])]))
 story.append(t)
def codigo(t):
 for line in t.splitlines():
  wrapped=textwrap.wrap(line,100,replace_whitespace=False,drop_whitespace=False) or ['']
  story.append(Preformatted('\n'.join(wrapped),S['Codigo']))
def footer(c,doc):
 c.saveState(); c.setStrokeColor(colors.HexColor('#B8C9D3')); c.line(56,44,539,44)
 c.setFont('Vera',7); c.setFillColor(colors.HexColor('#52677A'))
 c.drawString(56,31,'Escola Atenta | Auditoria de segurança | 08/09/2026')
 c.drawRightString(539,31,str(doc.page)); c.restoreState()
add('RELATÓRIO TÉCNICO','Sub'); story.append(Spacer(1,38))
add('Relatório de Auditoria de Segurança — Escola Atenta','Titulo')
add('Cinco categorias • evidências verificadas • rascunhos de issues','Sub')
add('08 de setembro de 2026'); add('Revisão: '+D['commit'],'Pequeno'); add('Branch: '+D['branch'],'Pequeno')
story.append(Spacer(1,22)); add('Escopo e método','Sub')
add('API ASP.NET Core 9, handlers MediatR, EF Core/SQLite, app React Native/Expo/WatermelonDB e configuração de deploy Windows. Revisão do código atual, histórico Git local e sourcemap disponível. Reproduções somente com dados sintéticos e SQLite em memória.')
add('Mapeamento das categorias: isolamento = UsuarioTurmas e cache offline; permissões = JWT, atributos Authorize e regras nos handlers; IDOR = IDs de rotas/body/SyncLog; segredos = configuração, histórico e artefatos; XSS = entrada de dados até destinos HTML/JavaScript ou UI nativa.')
add('Foram inventariadas 28 ações HTTP e 29 classes de handler, mais operações diretas de autenticação e workers. Estado inicial limpo. O produto não foi modificado; issues não foram publicadas.')
add('Leitura dos resultados','Sub'); add('Há achados reproduzidos e achados estáticos condicionais. As condições de cada um são explícitas. Ausência de achado não equivale a prova de segurança; as lacunas constam em Cobertura e limitações.')
story.append(PageBreak()); titulo('Resumo executivo')
counts=collections.Counter(x['severidade'] for x in F)
add(f'{len(F)} achados verificados: '+', '.join(f'{counts[s]} {s}' for s in COL)+'.')
chart=Drawing(480,155)
if F:
 pie=Pie(); pie.x=20; pie.y=8; pie.width=130; pie.height=130; pie.data=[counts[s] for s in COL if counts[s]]; pie.labels=['']*len(pie.data)
 for k,s in enumerate(s for s in COL if counts[s]): pie.slices[k].fillColor=colors.HexColor(COL[s]); pie.slices[k].strokeColor=colors.white
 chart.add(pie); chart.add(Circle(85,73,40,fillColor=colors.white,strokeColor=None)); chart.add(String(85, seventy:=72,str(len(F)),fontName='VeraBold',fontSize=26,textAnchor='middle')); chart.add(String(85,55,'achados',fontName='Vera',fontSize=8,textAnchor='middle'))
else: chart.add(String(25,75,'0 achados verificados',fontName='VeraBold',fontSize=17))
for j,s in enumerate(COL):
 chart.add(Rect(190,126-j*23,10,10,fillColor=colors.HexColor(COL[s]),strokeColor=None)); chart.add(String(210,127-j*23,f'{s.capitalize()}: {counts[s]}',fontName='Vera',fontSize=10))
story.append(chart)
add('Achados por categoria','Sub'); bars=Drawing(480,135)
for j,cat in enumerate(D['categorias']):
 n=sum(x['categoria']==cat['id'] for x in F); y=110-j*23
 name=cat['nome']+(' (parcial)' if cat['id']==4 else '')
 bars.add(String(0,y+2,name,fontName='Vera',fontSize=8.5)); bars.add(Rect(225,y, n*47,12,fillColor=colors.HexColor('#1C6475'),strokeColor=None)); bars.add(String(232+n*47,y+2,str(n),fontName='VeraBold',fontSize=9))
story.append(bars)
add('Risco central: o escopo por turma não é uniforme entre consultas, sync e escritas. Além disso, alterações de segurança da conta não encerram todas as sessões. Priorizar os controles compartilhados e a validação por objeto.')
add(D['testes'],'Pequeno'); add('As contagens excluem riscos latentes e consultas cuja exploração não foi confirmada. EA-07 depende de Development; EA-08 depende das condições de instalação descritas.','Pequeno')
story.append(PageBreak()); titulo('Pontos fortes e pontos fracos')
for ref,t in D['pontos_fortes']: add(t,'Sub'); add(ref,'Pequeno')
add('Pontos fracos prioritários','Sub'); add('Leituras sem escopo de turma (EA-01), cache sem separação de conta (EA-02), regras distintas entre REST e sync (EA-03), resolução de alerta sem vínculo (EA-04) e ciclo de vida de sessão incompleto (EA-05/EA-06). Segredos de desenvolvimento e permissões locais ampliam o risco nas condições de EA-07/EA-08.')
for x in F:
 story.append(PageBreak()); titulo(f'{x["id"]} | {x["titulo"]}')
 chip=Table([[p(x['severidade'].upper(),'Pequeno'),p('Categoria '+str(x['categoria'])+' | '+D['categorias'][x['categoria']-1]['nome'],'Pequeno')]],colWidths=[65,416]); chip.setStyle(TableStyle([('BACKGROUND',(0,0),(0,0),colors.HexColor(COL[x['severidade']])),('VALIGN',(0,0),(-1,-1),'TOP')]))
 # Texto preto sobre laranja/amarelo para manter contraste e redundância textual.
 story.append(chip); story.append(Spacer(1,10))
 add(x['descricao']); add('Pré-condições e explorabilidade','Sub'); add(x['condicoes']); add('Impacto','Sub'); add(x['impacto'])
 tab([['Severidade','Arquivo:linha','Descrição']]+[[x['severidade'],f'{z["arquivo"]}:{z["inicio"]}-{z["fim"]}',x['id']] for z in x['locais']],[54,374,53])
 add('Trecho mínimo representativo','Sub'); codigo(x['trecho']); add('Verificação','Sub'); add(x['verificacao']); add('Correção e aceite','Sub'); add(x['correcao']);
 if x['id']=='EA-08': add('Referência técnica: Inno Setup, [Dirs] / Permissions / readexec: https://jrsoftware.org/ishelp/topic_dirssection.htm','Pequeno')
 add('Critérios de aceite específicos no rascunho de issue correspondente, ao final do relatório.','Pequeno')
story.append(PageBreak()); titulo('Cobertura e limitações')
tab([['Categoria','Resultado']]+[[x['nome'],x['status']] for x in D['categorias']],[145,336])
add('Inventário completo','Sub'); add('Consulte cobertura.md para cada handler/operação, arquivo:linha, origem dos IDs, controle efetivo e resultado. dados.json contém 33 entradas detalhadas de cobertura e as 28 rotas HTTP. inventario.json registra os 705 arquivos textuais pesquisados, hashes e resultados redigidos; inventário não significa leitura manual integral de cada arquivo.')
for s in D['limitacoes']: add('• '+s,'Pequeno')
story.append(PageBreak()); titulo('Recomendações priorizadas')
for prioridade,ids,t in [('P1','EA-01, EA-03, EA-04','Unificar autorização por papel e por turma em todas as superfícies da API, incluindo pull e operações REST.'),('P1','EA-06','Revogar sessões em mudanças de segurança e impedir uso de privilégios antigos.'),('P1 condicional','EA-07, EA-08','Eliminar a chave conhecida de Development e restringir leitura de segredos na instalação Windows; rotacionar instâncias afetadas.'),('P2','EA-02','Segregar armazenamento e fila offline por servidor e identidade, preservando pendências.'),('P2','EA-05','Aplicar a troca obrigatória no servidor e restaurar o estado correto no app.')]:
 add(f'{prioridade} | {ids}','Sub'); add(t)
add('Prioridade indica ordem de execução; severidade indica impacto e explorabilidade. Validar em ambiente sintético: negativo sem vínculo/papel, positivo autorizado, mesma operação online/offline e mudança de identidade com pendências. Não publicar os rascunhos sem revisão do responsável.')
add('Regeneração','Sub'); add('python docs/security-audit/gerar_relatorio.py. Dados, fontes e rascunhos são locais. README.md documenta dependências, comandos e limitações. O script de geração não consulta o repositório nem a internet.')
story.append(PageBreak()); titulo('ISSUES PARA O GITHUB')
add('Rascunhos em Markdown literal, também disponíveis em issues.md. Nenhuma issue foi publicada. Os blocos abaixo são selecionáveis; quebras visuais de linha podem ser recompostas ao copiar.','Pequeno')
issues=(O/'issues.md').read_text(encoding='utf-8').split('\n\n--- ISSUE ')
for j,issue in enumerate(issues):
 if j: story.append(PageBreak()); issue='--- ISSUE '+issue
 for line in issue.splitlines():
  wrapped=textwrap.wrap(line,103,replace_whitespace=False,drop_whitespace=False) or ['']
  story.append(Preformatted('\n'.join(wrapped),S['Codigo']))
fn=O/'relatorio-auditoria-seguranca.pdf'
SimpleDocTemplate(str(fn),pagesize=A4,rightMargin=56,leftMargin=56,topMargin=50,bottomMargin=58,title='Relatório de Auditoria de Segurança - Escola Atenta',author='Auditoria técnica local').build(story,onFirstPage=footer,onLaterPages=footer)
r=PdfReader(fn); text='\n'.join(z.extract_text() for z in r.pages)
assert all(x['id'] in text for x in F)
assert all(f'--- FIM ISSUE {i} ---' in text for i in range(1,len(F)+1))
assert 'ISSUES PARA O GITHUB' in text and 'Cobertura e limitações' in text
qa={'paginas':len(r.pages),'achados':len(F),'severidades':dict(counts),'categorias':{str(c['id']):sum(x['categoria']==c['id'] for x in F) for c in D['categorias']},'extracao_texto':'seções, 8 IDs e 8 blocos de issue encontrados; acentos preservados','visual':'pendente de rasterização'}
(O/'verificacao-pdf.json').write_text(json.dumps(qa,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(qa,ensure_ascii=False))
