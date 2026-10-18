# Beako Presence Linux 3.7.3

Extraia o ZIP completo, feche a versão anterior e execute `bash instalar.sh`.
Abra Beako Presence no menu ou execute ~/.local/bin/beako-presence.
Requer Python 3.9+, Tkinter e Bash. Não exige dependências gráficas adicionais.

## Interface
Cartões, painéis, busca e botões com cantos arredondados.
Títulos, margens e botões principais alinhados.
Removidos da interface os controles Testar no terminal e Editar caminho .exe.
Selecione jogos e clique em Iniciar atividade. Use Parar selecionados ou Parar todos para encerrar.
Jogos sem executável permanecem pesquisáveis, mas não podem iniciar até a DB informar um executável compatível.

## Catálogo e ícones
A DB DCGames continua como fonte principal e acompanha o pacote para uso offline.
Os caminhos completos da DB são preservados, inclusive win64/valorant-win64-shipping.exe.
Ícones consultados pelo ID público no Discord e salvos em ~/.config/beako-presence/icons.
Sem ícone disponível, o cartão mostra iniciais. --offline desativa downloads automáticos.
Ctrl+F foca a pesquisa; Escape limpa. Tab navega pelos controles; Enter ou espaço ativa botões.

## Validação
Interface renderizada e inspecionada no Windows; ícones, seleção, pesquisa e acionamento de botão verificados.
A aparência final e execução das sessões ainda precisam ser conferidas no Linux.
O programa cria processos locais e não garante detecção para todos os jogos.

## Ajustes 3.7.1
Filtro Todos removido; controles inferiores reposicionados sem a linha vazia. Ícone fornecido aplicado no cabeçalho, janela e atalho Linux, preservando a transparência original.

## Ajustes 3.7.1
Filtro Todos removido; controles inferiores reposicionados sem a linha vazia. Ícone fornecido aplicado no cabeçalho, janela e atalho Linux, preservando a transparência original.

## Desempenho 3.7.2
Cartões reutilizados entre pesquisas, carregamento inicial de 12 itens e mais itens ao rolar.
Redesenho de cantos somente quando as cores mudam. Cancelamento de pesquisas pendentes duplicadas.
A busca continua percorrendo todo o catálogo; 12 é o lote visual, não um limite de resultados.
Mesmo roteiro de dez pesquisas no Windows: média de 1351 ms antes e 161 ms depois, incluindo atualização da interface.
O ganho na sua máquina Linux pode variar. Verificados resultados, seleção, cliques após reuso, lista vazia, rolagem e ícones.

## Títulos 3.7.3
Nomes ficam em uma linha, com reticências calculadas pela largura real da fonte quando necessário. Atualização ao redimensionar e ao reutilizar cartões. Nome completo preservado no catálogo. Verificados GTA, Liberty, troca de pesquisas e nomes muito longos em 1000 e 1280 pixels de largura no Windows.
