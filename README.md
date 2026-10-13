# Beako Presence

Aplicativo experimental em C# (Windows Forms / .NET Framework 4.x) para gerenciar atividades de jogos detectadas pelo Discord.

## Recursos
- Catálogo pesquisável de jogos
- Lista de jogos marcados, com **Desmarcar** e **Limpar todos**
- Seleção de atividades e encerramento de sessões
- Ícones e imagens locais, além de cache

## Compilação
No Windows 10/11, abra `ABRIR.bat` para compilar usando o compilador C# do .NET Framework 4.x e executar o aplicativo.

Também é possível obter o executável produzido automaticamente em **Actions > Build Windows > Artifacts**. O workflow compila diretamente os arquivos deste repositório.

## Transparência e alertas de antivírus
O aplicativo atualmente **copia seu próprio executável** para criar processos com nomes de jogos. Esse comportamento pode provocar alertas de antivírus e não é uma integração oficial com Discord. **Não há garantia de que as detecções sejam falsos positivos.** Revise o código e verifique os executáveis antes de executá-los.

Não inclua credenciais, dados pessoais ou arquivos de sessão neste repositório. Uma versão futura pode substituir a simulação de processos por Rich Presence oficial do Discord.

## Licença
Nenhuma licença de redistribuição foi definida.
