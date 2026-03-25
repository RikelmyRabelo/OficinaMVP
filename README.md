<div align="center">
<img src="https://raw.githubusercontent.com/FortAwesome/Font-Awesome/6.x/svgs/solid/gears.svg" width="80" height="80" alt="Logo">
<h1>🚗 FREITAS AUTOCENTER</h1>
<p><strong>Engenharia de Software aplicada à Gestão Automotiva Profissional</strong></p>
<img src="https://img.shields.io/badge/.NET-10.0-512bd4?style=for-the-badge&logo=dotnet">
<img src="https://img.shields.io/badge/Blazor-WASM-512bd4?style=for-the-badge&logo=blazor">
<img src="https://img.shields.io/badge/SQLite-003B57?style=for-the-badge&logo=sqlite">
</div>

📖 Sobre o Ecossistema
Este sistema nasceu da necessidade de transformar processos manuais de oficinas em fluxos digitais. Diferente de softwares genéricos, o Freitas Autocenter foca na realidade do mecânico: a peça que o cliente traz de fora, o pagamento que é metade PIX e metade dinheiro, e aquele item de estoque que possui três códigos diferentes para a mesma aplicação.

🧩 Módulos e Regras de Negócio
Gestão de Ordens de Serviço
O coração do sistema permite o acompanhamento em tempo real desde o checkout do veículo até a finalização. A lógica de "Pagamento Misto" resolve um problema comum do setor, permitindo que cada centavo seja rastreado individualmente por método de pagamento, gerando dados reais para o fechamento de caixa.

Inteligência de Estoque e Patrimônio
A funcionalidade de "Itens Externos" é um diferencial estratégico. Ela permite que peças fornecidas pelo cliente sejam registradas na O.S. para fins de histórico e garantia, mas as exclui automaticamente dos cálculos de patrimônio líquido da oficina. Isso garante que o valor do inventário exibido no painel financeiro represente apenas o capital real investido pela empresa.

Segurança de Dados e Auditoria
Implementamos o conceito de Soft Delete através de uma lixeira inteligente. Nenhuma O.S. é apagada permanentemente no primeiro clique; elas são movidas para um estado de retenção, permitindo auditorias financeiras posteriores e prevenindo perdas por erro humano.

💻 Ambiente de Desenvolvimento
Para configurar o ambiente e replicar o projeto localmente, utilize a sequência de comandos abaixo no seu prompt de comando.

<div align="left">
<pre style="background: #1e1e1e; color: #d4d4d4; padding: 15px; border-radius: 8px; border: 1px solid #333; font-family: 'Cascadia Code', 'Courier New', monospace;">
<span style="color: #6a9955;"># Clone o repositório oficial</span>
<span style="color: #9cdcfe;">git clone</span> https://github.com/RikelmyRabelo/oficinamvp.git

<span style="color: #6a9955;"># Acesse o diretório do servidor</span>
<span style="color: #9cdcfe;">cd</span> OficinaAPI

<span style="color: #6a9955;"># Sincronize as tabelas do banco de dados</span>
<span style="color: #9cdcfe;">dotnet ef</span> database update

<span style="color: #6a9955;"># Inicie o backend e o frontend</span>
<span style="color: #9cdcfe;">dotnet run</span> --project OficinaAPI
<span style="color: #9cdcfe;">dotnet run</span> --project OficinaBlazor
</pre>

</div>

🧪 Ciclo de Testes
A integridade dos cálculos financeiros e as filtragens de estoque são validadas por uma suíte de testes de integração automatizados.

<div align="left">
<pre style="background: #1e1e1e; color: #d4d4d4; padding: 15px; border-radius: 8px; border: 1px solid #333; font-family: 'Cascadia Code', 'Courier New', monospace;">
<span style="color: #ce9178;">Microsoft (R) Test Execution Command Line Tool</span>
<span style="color: #9cdcfe;">dotnet test</span>

<span style="color: #b5cea8;">Total tests: 12. Passed: 12. Failed: 0. Skipped: 0.</span>
<span style="color: #b5cea8;">Test Run Successful.</span>
</pre>

</div>

<div align="center">
<sub>Status: 🟢 Em produção | Versão: 1.0.4-stable</sub>


<strong>Desenvolvido por Rikelmy Freitas</strong>
</div>
