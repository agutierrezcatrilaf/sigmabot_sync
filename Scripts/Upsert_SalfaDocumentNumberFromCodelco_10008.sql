-- Ida Codelco → SALFA: DocumentNumber destino vía @SalfaDocumentNumberFromCodelco
DECLARE @IdTrabajo INT = 10008;
DECLARE @Codelco   NVARCHAR(50) = '1207996652';
DECLARE @Salfa     NVARCHAR(50) = '1207996803';

UPDATE TransmittalSyncCampoProyecto
SET CampoOrigen = N'@SalfaDocumentNumberFromCodelco',
    EsObligatorio = 1,
    ValorDefault = NULL,
    Catalogo = NULL
WHERE IdTrabajo = @IdTrabajo
  AND ACXProjectIdOrigen = @Codelco
  AND ACXProjectIdDestino = @Salfa
  AND Campo = N'DocumentNumber';

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO TransmittalSyncCampoProyecto
        (IdTrabajo, ACXProjectIdOrigen, ACXProjectIdDestino, Campo, CampoOrigen, EsObligatorio, ValorDefault, Catalogo, Orden)
    VALUES
    (@IdTrabajo, @Codelco, @Salfa, N'DocumentNumber', N'@SalfaDocumentNumberFromCodelco', 1, NULL, NULL, 10);
END

SELECT Campo, CampoOrigen, EsObligatorio
FROM TransmittalSyncCampoProyecto
WHERE IdTrabajo = @IdTrabajo
  AND ACXProjectIdOrigen = @Codelco
  AND ACXProjectIdDestino = @Salfa
  AND Campo = N'DocumentNumber';

PRINT 'OK: DocumentNumber ida → @SalfaDocumentNumberFromCodelco';
