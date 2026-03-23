🔧 Freitas Autocenter - Sistema de Gestão de Oficina
Este é um sistema completo para gerenciamento de oficinas mecânicas, composto por uma Web API robusta e um frontend interativo em Blazor. O sistema permite o controle de ordens de serviço, fluxo de estoque, catálogo de mão de obra e gestão financeira.

🚀 Funcionalidades Principais
📋 Gestão de Ordens de Serviço (O.S.)
Abertura e Edição: Fluxo completo desde a entrada do veículo até a entrega.

Itens e Serviços: Adição de peças do estoque, itens avulsos ou serviços do catálogo.

Anexos: Suporte para upload de fotos e documentos (PDF/Imagens) vinculados à O.S.

Impressão: Geração de notas de serviço e comprovantes de saída de materiais formatados para impressão.

📦 Controle de Estoque e Catálogo
Inventário Inteligente: Controle de quantidade mínima com alertas visuais (repor/esgotado).

Valor Patrimonial: Cálculo automático do valor em prateleira e histórico de saídas.

Mão de Obra: Catálogo de serviços com preços sugeridos para agilizar a criação de orçamentos.

♻️ Sistema de Lixeira Inteligente
Exclusão Lógica (Soft Delete): Itens excluídos não desaparecem imediatamente do banco de dados.

Restauração: Possibilidade de recuperar ordens de serviço excluídas por engano.

Limpeza Automática: O sistema remove permanentemente itens que estão na lixeira há mais de 30 dias.

Exclusão Definitiva: Opção para apagar dados permanentemente de forma manual.

💰 Financeiro
Pagamentos Mistos: Registro de pagamentos utilizando múltiplas formas (PIX, Dinheiro, Cartão) em uma única O.S.

Gestão de Inadimplência: Alertas de cobrança para clientes com pagamentos pendentes ou atrasados.

Resumo Financeiro: Dashboard com faturamento total, semanal e saldo em caixa.

🛠️ Tecnologias Utilizadas
Backend (OficinaAPI)
C# / .NET 8

Entity Framework Core: Mapeamento objeto-relacional.

SQL Server: Base de dados relacional.

ASP.NET Core Web API: Endpoints RESTful.

Frontend (OficinaWeb)
Blazor WebAssembly / Server

Bootstrap 5: Estilização e componentes.

Bootstrap Icons: Biblioteca de ícones.

HttpClient: Integração com a API.

📂 Estrutura do Banco de Dados (Principais Entidades)
ServiceOrders: Tabela principal que armazena dados da O.S., status e valores.

Products: Gerencia o estoque de peças.

ServiceItems: Itens (peças ou serviços) vinculados a uma O.S.

Vehicles: Cadastro de veículos e clientes.

ServiceOrderPayments: Detalha os múltiplos pagamentos de uma venda.

CashTransactions: Registra entradas e saídas manuais do caixa.

⚙️ Configuração do Ambiente
Pré-requisitos
SDK do .NET 8.0 ou superior.

SQL Server (LocalDB ou Express).

Visual Studio 2022 ou VS Code.

Passo a Passo
Clonar o Repositório:
git clone https://github.com/seu-usuario/oficina-mvp.git

Configurar a API:
Navegue até a pasta OficinaAPI.
Atualize a ConnectionString no arquivo appsettings.json.

Execute as migrações para criar o banco de dados:
dotnet ef database update

Inicie a API:
dotnet run

Configurar o Frontend:
Navegue até a pasta OficinaWeb.
Certifique-se de que a BaseAddress no Program.cs aponta para a URL da API (ex: https://localhost:7082).

Inicie o projeto:
dotnet run

📈 Fluxo de Trabalho (Git)
O projeto segue um fluxo de trabalho baseado em branches para garantir a estabilidade da main:

Feature: feature/nome-da-funcionalidade

Fix: fix/correcao-de-bug

Refactor: refactor/limpeza-de-codigo

Desenvolvido para Freitas Autocenter.
