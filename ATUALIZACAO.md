# Guia de Atualização do Escola Atenta

Este documento descreve o procedimento seguro para atualizar a API/servidor e o aplicativo Android do **Escola Atenta** sem perder o banco de dados, as chamadas já lançadas, os alunos, turmas, usuários e alertas.

> ⚠️ **Regra de ouro:** o banco de dados do servidor (`escolaatenta_local.db`) é o ativo mais importante. **Sempre faça backup antes de qualquer atualização.**

---

## 1. O que será atualizado

O sistema tem duas partes independentes:

1. **Servidor/API Windows** — instalado em um computador da escola. Guarda o banco de dados SQLite.
2. **Aplicativo Android** — instalado nos celulares dos monitores/supervisores. Trabalha offline e sincroniza com o servidor.

Em geral, atualize primeiro o servidor e depois os celulares, para garantir que os apps antigos ainda consigam sincronizar durante o rollout.

---

## 2. Localização do banco de dados

O banco do servidor fica ao lado do executável da API:

```
C:\EscolaAtenta\escolaatenta_local.db
```

Se o arquivo não estiver nesse caminho, procure na pasta onde o instalador colocou a API ou verifique o arquivo `appsettings.json` na chave `ConnectionStrings:DefaultConnection`.

> O banco usa **WAL mode**. Portanto, além do `.db`, podem existir temporariamente os arquivos `.db-wal` e `.db-shm`. Eles fazem parte do banco em uso e devem ser copiados juntos quando o serviço estiver **parado**.

---

## 3. Backup obrigatório antes de atualizar

### 3.1 Pare o serviço Windows

Abra o **Prompt de Comando como Administrador** e execute:

```cmd
sc stop EscolaAtenta
```

Aguarde até aparecer a mensagem `SERVICE_STOP_PENDING` → `STOPPED`.

> Não faça backup enquanto o serviço estiver rodando. Se o banco estiver em WAL mode, o arquivo `.db` pode estar incompleto sem os arquivos `.db-wal`/`.db-shm`.

### 3.2 Copie o banco

No Explorador de Arquivos, acesse:

```
C:\EscolaAtenta\
```

Copie para outro local (pen drive, pasta de documentos, nuvem):

```
escolaatenta_local.db
escolaatenta_local.db-wal   (se existir)
escolaatenta_local.db-shm   (se existir)
```

Dê um nome claro ao backup, incluindo a data:

```
escolaatenta_local_backup_2026-08-21.db
```

### 3.3 Confirme que o backup está íntegro

O arquivo de backup deve ter tamanho próximo ao original (geralmente algumas centenas de KB a alguns MB). Se ficar muito pequeno (zero ou poucos KB), o backup foi feito com o serviço rodando ou o caminho está errado.

---

## 4. Atualização do servidor Windows

### 4.1 Usando o instalador completo (`EscolaAtenta-Setup.exe`)

1. Execute o instalador da nova versão.
2. O instalador substituirá os arquivos da API e do TrayMonitor em `C:\EscolaAtenta\`.
3. **O instalador NÃO deve apagar o banco**, mas, por segurança, mantenha o backup feito no passo 3.
4. Ao final da instalação, o serviço será iniciado automaticamente.
5. Verifique se o serviço subiu:

```cmd
sc query EscolaAtenta
```

### 4.2 Usando o pacote OTA (`update.zip`)

Se o TrayMonitor detectou uma atualização e você escolheu instalar:

1. O TrayMonitor fará o download do `update.zip`.
2. Ele copiará o próprio executável para `%TEMP%` e solicitará elevação (UAC).
3. O processo elevado substituirá os binários em `C:\EscolaAtenta\`.
4. **O banco não é alterado** durante esse processo.
5. Após a substituição, o serviço é reiniciado.

> Mesmo usando OTA, faça o backup manualmente antes de confirmar a atualização.

### 4.3 Após a atualização

A API executa `MigrateAsync()` no startup. Isso significa que:

- Tabelas novas serão criadas.
- Colunas novas serão adicionadas.
- Colunas antigas **não serão removidas automaticamente** se ainda contiverem dados ou forem necessárias para compatibilidade.
- O banco continuará com todos os alunos, turmas, chamadas e presenças.

Para confirmar que subiu corretamente, acesse no navegador:

```
http://localhost:5114/health
```

Deve retornar `Healthy`.

---

## 5. Atualização do aplicativo Android

### 5.1 Backup dos dados do app (recomendado)

Se o app já tiver chamadas sincronizadas, os dados já estão no servidor. Se houver chamadas **não sincronizadas** no celular, perder o app antes do sync pode perdê-las.

Sempre sincronize antes de desinstalar:

1. Abra o app.
2. Vá até a tela de sincronização e toque em **Sincronizar agora**.
3. Aguarde a confirmação.

### 5.2 Instale a nova versão

1. Desinstale a versão antiga do celular (ou instale por cima, se o APK tiver a mesma assinatura).
2. Instale o novo APK (`app-release.apk`).
3. Abra o app e configure o endereço do servidor novamente, se necessário.
4. Faça login com o mesmo usuário.
5. Toque em sincronizar para baixar turmas e alunos atualizados.

> Se você desinstalar o app **antes** de sincronizar, as chamadas pendentes no celular serão perdidas. Sincronize primeiro.

---

## 6. Cenários especiais

### 6.1 O instalador apagou ou substituiu o banco

Se após a instalação a API estiver em branco (sem turmas/alunos):

1. Pare o serviço:

```cmd
sc stop EscolaAtenta
```

2. Copie o backup de volta para `C:\EscolaAtenta\escolaatenta_local.db`.
3. Inicie o serviço:

```cmd
sc start EscolaAtenta
```

4. A API detectará o banco e aplicará apenas as migrations pendentes.

### 6.2 Banco corrompido após a atualização

Se a API não subir e os logs indicarem corrupção:

1. Pare o serviço.
2. Verifique se existem arquivos `.db-wal` e `.db-shm` e se estão do mesmo backup.
3. Restaure o backup mais recente **completo** (`.db`, `.db-wal`, `.db-shm`).
4. Inicie o serviço.

> Nunca delete os arquivos `.db-wal` ou `.db-shm` manualmente enquanto o serviço estiver parado, a menos que esteja restaurando um backup completo.

### 6.3 App antigo não consegue sincronizar após atualizar só o servidor

Isso pode acontecer se o contrato da API mudou e o app ainda não foi atualizado.

- Sempre mantenha compatibilidade de rollout: o servidor novo continua aceitando chamadas do app antigo por um período.
- Atualize os apps Android assim que possível após atualizar o servidor.

---

## 7. Checklist resumido

Antes de atualizar:

- [ ] Pare o serviço Windows (`sc stop EscolaAtenta`).
- [ ] Copie `escolaatenta_local.db` (e `.db-wal`/`.db-shm` se existirem) para um local seguro.
- [ ] Confirme o tamanho do backup.

Durante a atualização:

- [ ] Execute o instalador ou confirme o update OTA.
- [ ] Não desligue o computador no meio do processo.

Após a atualização:

- [ ] Verifique se o serviço subiu (`sc query EscolaAtenta`).
- [ ] Teste o health check (`http://localhost:5114/health`).
- [ ] Verifique se as turmas e alunos ainda aparecem.
- [ ] Sincronize o app Android e verifique se os dados estão consistentes.

---

## 8. Onde pedir ajuda

Se algo der errado e o banco não subir:

1. Não reinstale por cima sem backup.
2. Pare o serviço.
3. Preserve os arquivos `.db`, `.db-wal` e `.db-shm` atuais.
4. Entre em contato com o suporte técnico informando:
   - Versão anterior instalada.
   - Versão nova que tentou instalar.
   - Mensagem de erro nos logs (`C:\EscolaAtenta\Logs\`).
   - Tamanho e local do backup mais recente.
