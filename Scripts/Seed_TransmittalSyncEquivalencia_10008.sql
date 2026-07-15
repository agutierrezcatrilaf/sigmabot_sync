-- Equivalencias ProjectSync IdTrabajo 10008 (Codelco → SALFA).
-- Discipline: Especialidad_singleSelect (origen) → Discipline_singleSelect (destino).
-- TipoDocumento: TipoDeDocumento_singleSelect (origen) → TipoDeDocumento_singleSelect (destino).

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @IdTrabajo INT = 10008;
DECLARE @Codelco   NVARCHAR(50) = '1207996652';
DECLARE @Salfa     NVARCHAR(50) = '1207996803';

DECLARE @DestDiagrama NVARCHAR(200) = N'DPI-Diagrama de Procesos E Instrumentaci' + NCHAR(243) + N'n';
DECLARE @DestEspec    NVARCHAR(200) = N'ETT-Especificaci' + NCHAR(243) + N'n T' + NCHAR(233) + N'cnica';

-- Discipline (Especialidad Codelco → Disciplina SALFA)
MERGE TransmittalSyncEquivalencia AS t
USING (VALUES
    (@IdTrabajo, @Codelco, @Salfa, N'Discipline', N'OH - OLEO HIDRAULICA', N'Piping'),
    (@IdTrabajo, @Codelco, @Salfa, N'Discipline', N'CA - CANERIAS',        N'Piping'),
    (@IdTrabajo, @Codelco, @Salfa, N'Discipline', N'AR - ARQUITECTURA',    N'Arquitectura'),
    (@IdTrabajo, @Codelco, @Salfa, N'Discipline', N'EL - ELECTRICIDAD',    N'Electricidad'),
    (@IdTrabajo, @Codelco, @Salfa, N'Discipline', N'ES - ESTRUCTURAL',     N'Estructuras'),
    (@IdTrabajo, @Codelco, @Salfa, N'Discipline', N'CI - CIVIL',           N'Civil')
) AS s (IdTrabajo, ACXProjectIdOrigen, ACXProjectIdDestino, Tipo, ValorOrigen, ValorDestino)
ON  t.IdTrabajo = s.IdTrabajo
AND t.ACXProjectIdOrigen = s.ACXProjectIdOrigen
AND t.ACXProjectIdDestino = s.ACXProjectIdDestino
AND t.Tipo = s.Tipo
AND t.ValorOrigen = s.ValorOrigen
WHEN MATCHED THEN
    UPDATE SET ValorDestino = s.ValorDestino, Activo = 1, UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (IdTrabajo, ACXProjectIdOrigen, ACXProjectIdDestino, Tipo, ValorOrigen, ValorDestino)
    VALUES (s.IdTrabajo, s.ACXProjectIdOrigen, s.ACXProjectIdDestino, s.Tipo, s.ValorOrigen, s.ValorDestino);

-- TipoDocumento (Codelco → SALFA)
MERGE TransmittalSyncEquivalencia AS t
USING (VALUES
    (@IdTrabajo, @Codelco, @Salfa, N'TipoDocumento', N'201OH - P & ID',                         @DestDiagrama),
    (@IdTrabajo, @Codelco, @Salfa, N'TipoDocumento', N'206CA-PLANOS DE PIEZAS ESPECIALES',       N'PDD-Plano de Detalles'),
    (@IdTrabajo, @Codelco, @Salfa, N'TipoDocumento', N'500AR - ESQUEMAS',                        N'PDD-Plano de Detalles'),
    (@IdTrabajo, @Codelco, @Salfa, N'TipoDocumento', N'ESPEL - ESPECIFICACION',                  @DestEspec),
    (@IdTrabajo, @Codelco, @Salfa, N'TipoDocumento', N'HDDES - HOJA DE DATOS',                   N'HDD-Hoja de Datos'),
    (@IdTrabajo, @Codelco, @Salfa, N'TipoDocumento', N'MNLCI - MANUAL',                          N'MAN-Manual'),
    (@IdTrabajo, @Codelco, @Salfa, N'TipoDocumento', N'INDCP - INFORME DIARIO (SOLO PARA ESPECIALIDAD CP)', N'IAD-Informe de Avance Diario')
) AS s (IdTrabajo, ACXProjectIdOrigen, ACXProjectIdDestino, Tipo, ValorOrigen, ValorDestino)
ON  t.IdTrabajo = s.IdTrabajo
AND t.ACXProjectIdOrigen = s.ACXProjectIdOrigen
AND t.ACXProjectIdDestino = s.ACXProjectIdDestino
AND t.Tipo = s.Tipo
AND t.ValorOrigen = s.ValorOrigen
WHEN MATCHED THEN
    UPDATE SET ValorDestino = s.ValorDestino, Activo = 1, UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (IdTrabajo, ACXProjectIdOrigen, ACXProjectIdDestino, Tipo, ValorOrigen, ValorDestino)
    VALUES (s.IdTrabajo, s.ACXProjectIdOrigen, s.ACXProjectIdDestino, s.Tipo, s.ValorOrigen, s.ValorDestino);

SELECT Tipo, ValorOrigen, ValorDestino
FROM TransmittalSyncEquivalencia
WHERE IdTrabajo = @IdTrabajo AND Activo = 1
ORDER BY Tipo, ValorOrigen;

PRINT 'TransmittalSyncEquivalencia: seed IdTrabajo=10008 (Discipline + TipoDocumento).';
GO
