-- Estado de ProjectSync por transmitals: mails ya procesados y mapeo DocumentNo+Revision → DocumentId local.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TransmittalSyncProcesados')
BEGIN
    CREATE TABLE [dbo].[TransmittalSyncProcesados] (
        Id              INT             IDENTITY(1,1) NOT NULL PRIMARY KEY,
        IdTrabajo       INT             NOT NULL,
        ACXProjectId    NVARCHAR(50)    NOT NULL,
        MailId          NVARCHAR(50)    NOT NULL,
        ProcessedAt     DATETIME2       NOT NULL CONSTRAINT DF_TransmittalSyncProcesados_ProcessedAt DEFAULT (SYSUTCDATETIME())
    );

    CREATE UNIQUE INDEX UX_TransmittalSyncProcesados_Trabajo_Proyecto_Mail
        ON [dbo].[TransmittalSyncProcesados] (IdTrabajo, ACXProjectId, MailId);

    PRINT 'Tabla TransmittalSyncProcesados creada.';
END
ELSE
    PRINT 'La tabla TransmittalSyncProcesados ya existe.';

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TransmittalSyncMapeo')
BEGIN
    CREATE TABLE [dbo].[TransmittalSyncMapeo] (
        Id              INT             IDENTITY(1,1) NOT NULL PRIMARY KEY,
        IdTrabajo       INT             NOT NULL,
        ACXProjectId    NVARCHAR(50)    NOT NULL,
        DocumentNo      NVARCHAR(100)   NOT NULL,
        Revision        NVARCHAR(20)    NOT NULL,
        LocalDocumentId NVARCHAR(50)    NOT NULL,
        UpdatedAt       DATETIME2       NOT NULL CONSTRAINT DF_TransmittalSyncMapeo_UpdatedAt DEFAULT (SYSUTCDATETIME())
    );

    CREATE UNIQUE INDEX UX_TransmittalSyncMapeo_Trabajo_Proyecto_DocRev
        ON [dbo].[TransmittalSyncMapeo] (IdTrabajo, ACXProjectId, DocumentNo, Revision);

    PRINT 'Tabla TransmittalSyncMapeo creada.';
END
ELSE
    PRINT 'La tabla TransmittalSyncMapeo ya existe.';
