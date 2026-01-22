using SigmabotSync.Domain.Models;
using SigmabotSync.Infrastructure.External;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigmabotSync.Application.Synchronization
{
    public class DocumentService
    {
        private readonly AconexDocumentClient _client;

        public DocumentService(AconexDocumentClient client)
        {
            _client = client;
        }

        public Task<List<DocumentIntegrityInfo>> GetChangedDocumentsAsync(string projectId, DateTime since)
        {
            return _client.GetChangedDocumentsAsync(projectId, since);
        }

        public Task<DocumentMetadata> GetDocumentMetadataAsync(string projectID, string id)
        {
            return _client.GetDocumentMetadataAsync(projectID, id);
        }
    }
}
