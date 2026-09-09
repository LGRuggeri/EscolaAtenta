"""Inventário local e busca heurística de segredos, com saída sem valores sensíveis.
Executar: python docs/security-audit/inventariar.py
Não valida credenciais remotamente. Histórico: todos os commits alcançáveis por refs locais.
"""
import json, re, subprocess, pathlib, hashlib
BASE=pathlib.Path(__file__).resolve().parents[2]
OUT=pathlib.Path(__file__).resolve().parent
EXCL={'node_modules','.git','bin','obj','build','.gradle','.expo','security-audit'}
def git(*args):
    return subprocess.check_output(['git',*args],cwd=BASE)
def permitido(p):
    return not any(s in EXCL for s in pathlib.PurePosixPath(p).parts)
rx=re.compile(r'(?i)(secretkey|api[_-]?key|password|senha|token|private[_-]?key|connectionstring|client[_-]?secret|signingkey)')
sig=re.compile(r'(?:gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,}|sk-[A-Za-z0-9_-]{20,}|AKIA[A-Z0-9]{16}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+)')
assign=re.compile(r'''(?i)["']?([\w:.-]*(?:secret|password|senha|token|api[_-]?key|hashsenha)[\w:.-]*)["']?\s*[:=]\s*["']([^"'\r\n]*)["']''')
def ocorrencias(text):
    arr=[]
    for n,line in enumerate(text.splitlines(),1):
        m=assign.search(line)
        if m:
            val=m.group(2)
            arr.append({'linha':n,'variavel':m.group(1),'estado':'vazio' if not val else 'literal_redigido','comprimento':len(val)})
        elif sig.search(line):
            arr.append({'linha':n,'variavel':'assinatura_de_credencial','estado':'redigido'})
    return arr
arquivos=[]; atuais=[]; xs=[]
sink=re.compile(r'innerHTML|dangerouslySetInnerHTML|v-html|WebView|\beval\s*\(|new\s+Function\s*\(|javascript:|Html\.Raw|WriteLiteral|text/html|<iframe|srcDoc|document\.write|renderToString|printToFileAsync|markdown',re.I)
for p in BASE.rglob('*'):
    rel=p.relative_to(BASE).as_posix()
    if not p.is_file() or not permitido(rel): continue
    if p.stat().st_size>3000000: continue
    try: text=p.read_text(encoding='utf-8-sig')
    except (UnicodeError,OSError): continue
    arquivos.append({'arquivo':rel,'linhas':len(text.splitlines()),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
    found=ocorrencias(text)
    if found: atuais.append({'arquivo':rel,'ocorrencias':found})
    for n,line in enumerate(text.splitlines(),1):
        if sink.search(line): xs.append({'arquivo':rel,'linha':n,'tipo':sink.search(line).group(0)})
commits=git('rev-list','--all').decode().splitlines(); vistos=set(); historico=[]
for commit in commits:
    for row in git('ls-tree','-r',commit).decode().splitlines():
        meta,p=row.split('\t',1); mode,typ,blob=meta.split()
        if typ!='blob' or not permitido(p) or blob in vistos: continue
        vistos.add(blob)
        raw=git('cat-file','blob',blob)
        if len(raw)>3000000 or b'\x00' in raw: continue
        try: text=raw.decode('utf-8-sig')
        except UnicodeError: continue
        found=ocorrencias(text)
        if found: historico.append({'commit':commit,'arquivo':p,'blob':blob,'ocorrencias':found})
res={'commit':git('rev-parse','HEAD').decode().strip(),'branch':git('branch','--show-current').decode().strip(),'commits':len(commits),'blobs_unicos_inspecionados':len(vistos),'arquivos':arquivos,'segredos_atuais_candidatos':atuais,'segredos_historicos_candidatos':historico,'sinks_xss_candidatos':xs,'limites':['Heurísticas não são prova de ausência de segredos.','Refs locais apenas; sem fetch, reflog, objetos inalcançáveis ou validação remota.','Binários e arquivos acima de 3 MB não inspecionados por conteúdo.','Candidatos de teste, placeholders e nomes de chaves não são automaticamente achados.']}
(OUT/'inventario.json').write_text(json.dumps(res,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'commits':len(commits),'blobs':len(vistos),'arquivos':len(arquivos),'atuais':atuais,'historico_arquivos':sorted(set(x['arquivo'] for x in historico)),'sinks_xss':xs},ensure_ascii=False))
