/* =====================================================================
   Monitor de Sala Cirúrgica: banco SQL Server
   ---------------------------------------------------------------------
   Cria o banco, as três tabelas (Limite, ConfiguracaoSala, AlertaLog),
   as stored procedures usadas pelos DAOs (padrões:
   spInsert_Tabela, spUpdate_Tabela, spDelete, spConsulta, spListagem e
   spProximoId) e os dados iniciais (perfis "Norma NBR 7256" e "Demonstração").

   Como executar:
     Visual Studio > Exibir > Pesquisador de Objetos do SQL Server >
     (localdb)\MSSQLLocalDB > botão direito > Nova Consulta > colar e executar
     ou no SSMS, conectado ao servidor da connection string.

   O script pode ser executado de novo: ele apaga e recria tudo.
   ===================================================================== */

IF DB_ID('MonitorSalaCirurgica') IS NULL
    CREATE DATABASE MonitorSalaCirurgica;
GO
USE MonitorSalaCirurgica;
GO

/* ---------------------------------------------------------------------
   Limpeza
   --------------------------------------------------------------------- */
DROP PROCEDURE IF EXISTS spInsert_Limite, spUpdate_Limite, spListagemLimites, spListagemLimitesPorPerfil, spListagemPerfis,
                         spInsert_ConfiguracaoSala, spUpdate_ConfiguracaoSala,
                         spConsultaConfiguracaoPorDevice, spDeleteConfiguracaoPorDevice,
                         spInsert_AlertaLog, spUpdateStatus_AlertaLog, spConsultaAvancadaAlertas,
                         spDelete, spConsulta, spListagem, spProximoId;
DROP TABLE IF EXISTS AlertaLog, ConfiguracaoSala, Limite;
GO

/* =====================================================================
   TABELAS
   ===================================================================== */

-- Limite: faixa aceitável de cada grandeza dentro de um perfil.
-- Um perfil tem exatamente uma linha por grandeza (UNIQUE Perfil + Grandeza).
-- Histerese: margem para SAIR do alerta. Ex.: máximo 28 °C e histerese 0,5:
--   entra em alerta com média > 28,0 e só sai com média <= 27,5.
-- O Id é gerado pela aplicação (spProximoId), como a chave dos demais cadastros.
CREATE TABLE Limite (
    Id        INT           NOT NULL PRIMARY KEY,
    Perfil    NVARCHAR(50)  NOT NULL,
    Grandeza  VARCHAR(20)   NOT NULL,
    Minimo    DECIMAL(6,2)  NOT NULL,
    Maximo    DECIMAL(6,2)  NOT NULL,
    Histerese DECIMAL(6,2)  NOT NULL CONSTRAINT DF_Limite_Histerese DEFAULT 0,
    CONSTRAINT UQ_Limite_Perfil_Grandeza UNIQUE (Perfil, Grandeza),
    CONSTRAINT CK_Limite_Grandeza  CHECK (Grandeza IN ('temperatura', 'umidade', 'luminosidade')),
    CONSTRAINT CK_Limite_Faixa     CHECK (Minimo < Maximo),
    CONSTRAINT CK_Limite_Histerese CHECK (Histerese >= 0)
);

-- ConfiguracaoSala: o que a APLICAÇÃO precisa saber de cada sala.
-- O dispositivo em si (atributos, comando, apikey) fica no IoT Agent do FIWARE;
-- aqui ficam o nome, o perfil de limites e a janela da média.
CREATE TABLE ConfiguracaoSala (
    Id              INT IDENTITY(1,1) PRIMARY KEY,
    DeviceId        VARCHAR(50)   NOT NULL,   -- device_id no IoT Agent (ex.: sala01)
    Nome            NVARCHAR(80)  NOT NULL,   -- nome nas telas e na fala da Alexa
    PerfilAtivo     NVARCHAR(50)  NOT NULL,   -- valor de Limite.Perfil
    JanelaSegundos  INT           NOT NULL CONSTRAINT DF_Config_Janela DEFAULT 30,
    SupervisaoAtiva BIT           NOT NULL CONSTRAINT DF_Config_Supervisao DEFAULT 1,
    CONSTRAINT UQ_ConfiguracaoSala_Device UNIQUE (DeviceId),
    CONSTRAINT CK_Config_Janela CHECK (JanelaSegundos BETWEEN 10 AND 3600)
);

-- AlertaLog: cada comando setAlert enviado ao ESP32 e o que aconteceu com ele.
-- Origem: Supervisão (automático), Teste (botão da tela Salas), Alexa, Silêncio.
-- Status: Enviado -> Confirmado (o ESP32 publicou o mesmo "alert") ou Reenviado / Falha.
CREATE TABLE AlertaLog (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    DataHora DATETIME2(0)   NOT NULL,
    DeviceId VARCHAR(50)    NOT NULL,
    Alerta   VARCHAR(10)    NOT NULL,
    Origem   NVARCHAR(20)   NOT NULL,
    Motivo   NVARCHAR(300)  NULL,
    Status   NVARCHAR(20)   NOT NULL,
    CONSTRAINT CK_AlertaLog_Alerta CHECK (Alerta IN ('luz', 'temp', 'umid', 'off'))
);
CREATE INDEX IX_AlertaLog_Device_Data ON AlertaLog (DeviceId, DataHora DESC);
GO

/* =====================================================================
   PROCEDURES GENÉRICAS (usadas pelo PadraoDAO, iguais para todas as tabelas)
   O nome da tabela vem sempre do código (SetTabela do DAO); mesmo assim,
   as procedures conferem se a tabela existe e usam QUOTENAME.
   ===================================================================== */
CREATE PROCEDURE spDelete (@id INT, @tabela VARCHAR(MAX))
AS
BEGIN
    IF OBJECT_ID(@tabela, 'U') IS NULL THROW 50001, 'Tabela inexistente.', 1;
    DECLARE @sql NVARCHAR(MAX) = N'DELETE FROM ' + QUOTENAME(@tabela) + N' WHERE Id = @id';
    EXEC sp_executesql @sql, N'@id INT', @id = @id;
END
GO

CREATE PROCEDURE spConsulta (@id INT, @tabela VARCHAR(MAX))
AS
BEGIN
    IF OBJECT_ID(@tabela, 'U') IS NULL THROW 50001, 'Tabela inexistente.', 1;
    DECLARE @sql NVARCHAR(MAX) = N'SELECT * FROM ' + QUOTENAME(@tabela) + N' WHERE Id = @id';
    EXEC sp_executesql @sql, N'@id INT', @id = @id;
END
GO

-- @ordem: número da coluna (ex.: '1') ou nome de uma coluna da tabela
CREATE PROCEDURE spListagem (@tabela VARCHAR(MAX), @ordem VARCHAR(MAX))
AS
BEGIN
    IF OBJECT_ID(@tabela, 'U') IS NULL THROW 50001, 'Tabela inexistente.', 1;
    DECLARE @porOrdem NVARCHAR(200) =
        CASE WHEN @ordem NOT LIKE '%[^0-9]%' AND LEN(@ordem) BETWEEN 1 AND 2 THEN @ordem
             WHEN COL_LENGTH(@tabela, @ordem) IS NOT NULL THEN QUOTENAME(@ordem)
             ELSE '1' END;
    DECLARE @sql NVARCHAR(MAX) = N'SELECT * FROM ' + QUOTENAME(@tabela) + N' ORDER BY ' + @porOrdem;
    EXEC sp_executesql @sql;
END
GO

CREATE PROCEDURE spProximoId (@tabela VARCHAR(MAX))
AS
BEGIN
    IF OBJECT_ID(@tabela, 'U') IS NULL THROW 50001, 'Tabela inexistente.', 1;
    DECLARE @sql NVARCHAR(MAX) = N'SELECT ISNULL(MAX(Id) + 1, 1) FROM ' + QUOTENAME(@tabela);
    EXEC sp_executesql @sql;
END
GO

/* =====================================================================
   LIMITE
   ===================================================================== */
CREATE PROCEDURE spInsert_Limite
    (@id INT, @perfil NVARCHAR(50), @grandeza VARCHAR(20),
     @minimo DECIMAL(6,2), @maximo DECIMAL(6,2), @histerese DECIMAL(6,2))
AS
BEGIN
    INSERT INTO Limite (Id, Perfil, Grandeza, Minimo, Maximo, Histerese)
    VALUES (@id, @perfil, @grandeza, @minimo, @maximo, @histerese);
END
GO

CREATE PROCEDURE spUpdate_Limite
    (@id INT, @perfil NVARCHAR(50), @grandeza VARCHAR(20),
     @minimo DECIMAL(6,2), @maximo DECIMAL(6,2), @histerese DECIMAL(6,2))
AS
BEGIN
    UPDATE Limite
       SET Perfil = @perfil, Grandeza = @grandeza, Minimo = @minimo,
           Maximo = @maximo, Histerese = @histerese
     WHERE Id = @id;
END
GO

-- Listagem da tela Limites: agrupada por perfil
CREATE PROCEDURE spListagemLimites (@tabela VARCHAR(MAX), @ordem VARCHAR(MAX))
AS
BEGIN
    SELECT * FROM Limite ORDER BY Perfil, Grandeza;
END
GO

-- Limites de um perfil (o SupervisorService chama a cada ciclo)
CREATE PROCEDURE spListagemLimitesPorPerfil (@perfil NVARCHAR(50))
AS
BEGIN
    SELECT * FROM Limite WHERE Perfil = @perfil ORDER BY Grandeza;
END
GO

-- Perfis existentes (combos das telas Salas e Limites)
CREATE PROCEDURE spListagemPerfis
AS
BEGIN
    SELECT Perfil, COUNT(*) AS Grandezas FROM Limite GROUP BY Perfil ORDER BY Perfil;
END
GO

/* =====================================================================
   CONFIGURACAOSALA (Id IDENTITY: @id é recebido e ignorado no insert,
   para o DAO mandar os mesmos parâmetros no insert e no update)
   ===================================================================== */
CREATE PROCEDURE spInsert_ConfiguracaoSala
    (@id INT, @deviceId VARCHAR(50), @nome NVARCHAR(80), @perfilAtivo NVARCHAR(50),
     @janelaSegundos INT, @supervisaoAtiva BIT)
AS
BEGIN
    INSERT INTO ConfiguracaoSala (DeviceId, Nome, PerfilAtivo, JanelaSegundos, SupervisaoAtiva)
    VALUES (@deviceId, @nome, @perfilAtivo, @janelaSegundos, @supervisaoAtiva);
END
GO

CREATE PROCEDURE spUpdate_ConfiguracaoSala
    (@id INT, @deviceId VARCHAR(50), @nome NVARCHAR(80), @perfilAtivo NVARCHAR(50),
     @janelaSegundos INT, @supervisaoAtiva BIT)
AS
BEGIN
    UPDATE ConfiguracaoSala
       SET DeviceId = @deviceId, Nome = @nome, PerfilAtivo = @perfilAtivo,
           JanelaSegundos = @janelaSegundos, SupervisaoAtiva = @supervisaoAtiva
     WHERE Id = @id;
END
GO

CREATE PROCEDURE spConsultaConfiguracaoPorDevice (@deviceId VARCHAR(50))
AS
BEGIN
    SELECT * FROM ConfiguracaoSala WHERE DeviceId = @deviceId;
END
GO

CREATE PROCEDURE spDeleteConfiguracaoPorDevice (@deviceId VARCHAR(50))
AS
BEGIN
    DELETE FROM ConfiguracaoSala WHERE DeviceId = @deviceId;
END
GO

/* =====================================================================
   ALERTALOG
   ===================================================================== */
CREATE PROCEDURE spInsert_AlertaLog
    (@id INT, @dataHora DATETIME2(0), @deviceId VARCHAR(50), @alerta VARCHAR(10),
     @origem NVARCHAR(20), @motivo NVARCHAR(300), @status NVARCHAR(20))
AS
BEGIN
    INSERT INTO AlertaLog (DataHora, DeviceId, Alerta, Origem, Motivo, Status)
    VALUES (@dataHora, @deviceId, @alerta, @origem, @motivo, @status);
END
GO

-- Muda só a situação (Enviado -> Confirmado / Reenviado / Falha)
CREATE PROCEDURE spUpdateStatus_AlertaLog (@id INT, @status NVARCHAR(20))
AS
BEGIN
    UPDATE AlertaLog SET Status = @status WHERE Id = @id;
END
GO

-- Histórico de alertas com filtros opcionais (NULL = sem filtro), como uma consulta avançada com filtros
CREATE PROCEDURE spConsultaAvancadaAlertas
    (@deviceId VARCHAR(50), @origem NVARCHAR(20), @dataInicial DATETIME2(0), @dataFinal DATETIME2(0))
AS
BEGIN
    SELECT TOP (300) *
      FROM AlertaLog
     WHERE (@deviceId    IS NULL OR DeviceId = @deviceId)
       AND (@origem      IS NULL OR Origem = @origem)
       AND (@dataInicial IS NULL OR DataHora >= @dataInicial)
       AND (@dataFinal   IS NULL OR DataHora <= @dataFinal)
     ORDER BY DataHora DESC, Id DESC;
END
GO

/* =====================================================================
   DADOS INICIAIS
   ===================================================================== */

-- Perfil "Norma NBR 7256": faixas de referência para sala cirúrgica
-- (temperatura 18 a 22 °C, umidade relativa 45 a 55 %). Confira a edição da
-- norma adotada pela equipe. A NBR 7256 não trata de iluminação: o limite de
-- luz é definido pela equipe sobre a escala 0-100 % do LDR.
INSERT INTO Limite (Id, Perfil, Grandeza, Minimo, Maximo, Histerese) VALUES
    (1, N'Norma NBR 7256', 'temperatura', 18.00,  22.00, 0.50),
    (2, N'Norma NBR 7256', 'umidade',     45.00,  55.00, 2.00),
    (3, N'Norma NBR 7256', 'luminosidade', 60.00, 100.00, 5.00);

-- Perfil "Demonstração": faixas largas para a sala de aula, de modo que só a
-- provocação (segurar/aquecer o DHT11, soprar nele, cobrir o LDR) dispare o alarme.
-- Ajuste com os valores normais medidos pelo Analista 2 no Serial.
INSERT INTO Limite (Id, Perfil, Grandeza, Minimo, Maximo, Histerese) VALUES
    (4, N'Demonstração', 'temperatura',  15.00,  28.00, 0.50),
    (5, N'Demonstração', 'umidade',      30.00,  75.00, 2.00),
    (6, N'Demonstração', 'luminosidade', 25.00, 100.00, 3.00);

-- A sala do contrato (device_id sala01) já começa configurada
INSERT INTO ConfiguracaoSala (DeviceId, Nome, PerfilAtivo, JanelaSegundos, SupervisaoAtiva)
VALUES ('sala01', N'Sala 01', N'Demonstração', 30, 1);
GO

-- Conferência rápida
SELECT * FROM Limite ORDER BY Perfil, Grandeza;
SELECT * FROM ConfiguracaoSala;
GO
