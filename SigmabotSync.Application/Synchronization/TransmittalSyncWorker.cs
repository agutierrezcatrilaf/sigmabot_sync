using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SigmabotSync.Application.Synchronization
{
    /// <summary>
    /// Sincronización bidireccional por transmitals: lee inbox de cada proyecto del par y aplica marcadores/archivos.
    /// </summary>
    public sealed class TransmittalSyncWorker
    {
        private readonly TransmittalSyncService _service;

        public event Action<string> OnStatus;

        public TransmittalSyncWorker(TransmittalSyncService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public async Task RunAsync(TransmittalSyncRunRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var proyectos = request.Proyectos?.Where(p => p != null && !string.IsNullOrWhiteSpace(p.ProjectId)).ToList();
            if (proyectos == null || proyectos.Count == 0)
            {
                OnStatus?.Invoke("No hay proyectos configurados para sincronizar.");
                return;
            }

            OnStatus?.Invoke($"ProjectSync por transmitals: {proyectos.Count} proyecto(s), lookback {request.DiasLookback} días.");

            foreach (var proyecto in proyectos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                OnStatus?.Invoke($"--- Inbox: {proyecto.Label} ({proyecto.ProjectId}) ---");

                var result = await _service.ProcessProjectInboxAsync(
                    request,
                    proyecto,
                    msg => OnStatus?.Invoke(msg),
                    cancellationToken).ConfigureAwait(false);

                OnStatus?.Invoke(
                    $"Resumen {proyecto.Label}: mails={result.TotalMails}, procesados={result.ProcessedMails}, " +
                    $"omitidos={result.SkippedAlreadyProcessed}, marcadores={result.PlaceholdersCreated}, " +
                    $"archivos={result.FilesApplied}, errores={result.Errors}");
            }

            OnStatus?.Invoke("Sincronización por transmitals finalizada.");
        }
    }
}
