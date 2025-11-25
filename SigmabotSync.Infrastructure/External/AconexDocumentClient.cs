using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SigmabotSync.Infrastructure.External
{
    public class AconexDocumentClient : AconexClientBase
    {
        public AconexDocumentClient(string u, string p, string id) : base(u, p, id) { }

    //    public async Task<List<Document>> GetUpdatedDocumentsAsync()
    //    {
    //        using var client = CreateClient();
    //        ...
    //}

    //    public async Task<DocumentDetail> GetDocumentDetailAsync(string docId)
    //    {
    //        using var client = CreateClient();
    //        ...
    //}

    //    public async Task<bool> UpdateDocumentAsync(string docId, DocumentUpdate update)
    //    {
    //        using var client = CreateClient();
    //        ...
    //}
    }

}
