Freitas Autocenter - Sistema de Gestão de Oficina
Este sistema foi desenvolvido para o gerenciamento completo de oficinas mecânicas através de uma Web API robusta em C#/.NET 8 e um frontend interativo em Blazor. A solução integra o controle de ordens de serviço, fluxo de estoque com alertas de reposição, catálogo de mão de obra e gestão financeira detalhada.

Funcionalidades e Operação
O software permite a abertura e edição de ordens de serviço com suporte para inclusão de peças do estoque, itens avulsos e serviços do catálogo. Inclui suporte para upload de anexos e geração de notas formatadas para impressão. O estoque monitoriza quantidades mínimas e calcula automaticamente o valor patrimonial. Existe um sistema de lixeira inteligente que realiza a exclusão lógica, permitindo a restauração de dados ou a remoção permanente automática após 30 dias. O módulo financeiro suporta múltiplos métodos de pagamento e alertas de cobrança.

Configuração Técnica
A stack tecnológica utiliza Entity Framework Core com SQL Server no backend e Bootstrap 5 para a interface. Para configurar o ambiente, é necessário clonar o repositório e ajustar a ConnectionString no ficheiro appsettings da API. Utilize o comando dotnet ef database update para estruturar o banco de dados e execute a API e o Frontend sequencialmente através do comando dotnet run. O fluxo de trabalho baseia-se em branches específicas para novas funcionalidades e correções.
