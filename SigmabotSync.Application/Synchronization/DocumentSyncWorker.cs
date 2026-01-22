using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigmabotSync.Application.Synchronization
{
    public class DocumentSyncWorker
    {
        private readonly DocumentService _documentService;

        public event Action<int, int> OnProgress;
        public event Action<string> OnStatus;

        public DocumentSyncWorker(DocumentService documentService)
        {
            _documentService = documentService;
        }

        public async Task RunAsync(string projectId, DateTime since)
        {
            OnStatus?.Invoke("Buscando documentos modificados...");

            // 1) Obtener documentos modificados
            var changedDocs = await _documentService.GetChangedDocumentsAsync(projectId, since);

            int total = changedDocs.Count;
            OnStatus?.Invoke($"Documentos modificados encontrados: {total}");

            if (total == 0)
            {
                OnStatus?.Invoke("No hay documentos por sincronizar.");
                return;
            }

            int current = 0;

            // 2) Iterar documentos para procesarlos

            foreach (var doc in changedDocs)
            {
                current++;
                OnProgress?.Invoke(current, total);
                OnStatus?.Invoke($"Procesando documento {current} de {total} (ID={doc.Id})...");

                var metadata = await _documentService.GetDocumentMetadataAsync(projectId, doc.Id);

                // Después haremos:
                // await _documentService.UpdateDocumentOnDestinationAsync(metadata);
            }


            OnStatus?.Invoke("Sincronización finalizada.");
        }
    }

}
