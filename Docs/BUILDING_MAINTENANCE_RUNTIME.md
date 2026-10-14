# Manutenção de construções em runtime

`BuildingMaintenanceRuntimeBinding` conecta `BuildingRepairDemolitionService` ao prédio vivo e oferece um snapshot seguro para a interface. Reparos atualizam a vida e o feedback do prédio durante o avanço; demolições emitem `OperationCompleted` para que o dono da cena crie ruínas ou remova o objeto no momento correto.

O binding valida identidade do prédio, economia e especificação antes de criar o serviço. Ownership, reservas, reembolso, progresso e cancelamento continuam sob responsabilidade do serviço determinístico. Nenhum prefab novo é criado automaticamente.

O Unity licenciado ainda é necessário para confirmar a integração visual, a substituição por ruínas e o comportamento em cena.
