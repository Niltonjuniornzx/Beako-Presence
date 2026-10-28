# Beako Presence v3.7.6 — Windows 3.6.4 / Linux 3.7.3

## Windows 3.6.4

- A checagem de versão do Windows agora acontece automaticamente ao iniciar; a opção manual foi removida da interface.
- Ao abrir BeakoPresence.exe pela primeira vez dentro do ZIP completo, ele instala por usuário, cria atalhos na Área de Trabalho e no menu Iniciar e registra a desinstalação; não exige administrador.
- O ZIP também inclui INSTALAR.bat para reinstalar manualmente e DESINSTALAR.bat como alternativa.
- O atualizador baixa a release, valida o SHA-256, atualiza após fechar o app e o reabre; configurações, cache e logs são preservados.
- Catálogo atualizado com dados oficiais do Discord e caminhos comunitários documentados.
- Inclui a correção Steam do EA SPORTS FC 27 desta cópia.

Extraia `Beako-Presence-Windows-3.6.4.zip` e abra `BeakoPresence.exe`. Para atualizar manualmente, execute o mesmo arquivo do ZIP mais recente. Para remover, use Beako Presence > Desinstalar no menu Iniciar ou execute `DESINSTALAR.bat`. Requer Windows com .NET Framework 4.8.

## Linux 3.7.3

Pacote Linux mantido na versão 3.7.3. Extraia `Beako-Presence-Linux-3.7.3.zip` e execute `bash ./instalar.sh`. Requer Python 3.9+, Tkinter e Bash.

## Integridade

`SHA256SUMS.txt` contém os hashes SHA-256 dos downloads.

O reconhecimento dos processos como jogos depende da detecção do Discord e não é garantido para todos os títulos. O executável Windows não possui assinatura digital.

