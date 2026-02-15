-- Tabla Trabajos: estado y resultado de ejecución de cada trabajo.
-- id es IDENTITY; el registro debe existir (id 1 = primer trabajo, etc.).
-- Se actualiza al final de cada ejecución (ResultadoUltimaEjecucion, FechaUltimaEjecucion, etc.).

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Trabajos')
BEGIN
    CREATE TABLE [dbo].[Trabajos] (
        id                      INT             IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Nombre                  NVARCHAR(200)   NULL,
        Tipo                    NVARCHAR(100)   NULL,
        Perioricidad            NVARCHAR(100)   NULL,
        FechaUltimaEjecucion    DATETIME        NULL,
        FechaProximaEjecucion   DATETIME        NULL,
        ResultadoUltimaEjecucion NVARCHAR(50)   NULL,
        ControldeEjecucion      NVARCHAR(200)   NULL,
        Estado                  NVARCHAR(50)   NULL,
        UltCorrEjecucion        NVARCHAR(MAX)   NULL
    );

    CREATE INDEX IX_Trabajos_Estado ON [dbo].[Trabajos] (Estado);

    PRINT 'Tabla Trabajos creada.';
END
ELSE
    PRINT 'La tabla Trabajos ya existe.';

-- Ejemplo: insertar trabajo (id se asigna por IDENTITY; id=1 para el primer trabajo)
/*
INSERT INTO [dbo].[Trabajos] (Nombre, Tipo, Perioricidad, Estado)
VALUES ('Extracción documentos Aconex', 'ExtraccionArchivos', 'Diaria', 'Pendiente');
*/
