using IntegrationWorkers.Models;
using IntegrationWorkers.Models.Document;
using IntegrationWorkers.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrationWorkers.Services
{
    public class DocumentSyncWorker
    {
        private readonly Dictionary<string, string> _config;
        private readonly SqlConnection _dbConDocs;

        private DataTable DocumentosTmp;
        private DataTable Metadatatmp;

        public DocumentSyncWorker(Dictionary<string, string> config, string connectionString)
        {
            _config = config;
            _dbConDocs = new SqlConnection(connectionString);
        }

        public void Documentos(BackgroundWorker bgwdocs, string proyectID)
        {
            dbchecktmpTables();
            dbcleartmptables();
            datosactuales(proyectID);
            GetACXDocumentsAsync(proyectID, bgwdocs)
                .GetAwaiter()
                .GetResult();
            dbUpdateProjectData(proyectID);
        }

        private void dbchecktmpTables()
        {
            DocumentosTmp?.Clear();
            DocumentosTmp = null;

            Metadatatmp?.Clear();
            Metadatatmp = null;

            DocumentosTmp = new DataTable("Documentos_tmp");

            // Columnas del nuevo esquema

            DocumentosTmp.Columns.Add("Id", typeof(long));
            DocumentosTmp.Columns.Add("ACXProjectId", typeof(string));
            DocumentosTmp.Columns.Add("TrackingId", typeof(long));
            DocumentosTmp.Columns.Add("DocumentNumber", typeof(string));
            DocumentosTmp.Columns.Add("Title", typeof(string));
            DocumentosTmp.Columns.Add("Revision", typeof(string));
            DocumentosTmp.Columns.Add("AuthorisedBy", typeof(string));
            DocumentosTmp.Columns.Add("DocumentType", typeof(string));
            DocumentosTmp.Columns.Add("DocumentStatus", typeof(string));
            DocumentosTmp.Columns.Add("Comments", typeof(string));
            DocumentosTmp.Columns.Add("Discipline", typeof(string));
            DocumentosTmp.Columns.Add("PrintSize", typeof(string));
            DocumentosTmp.Columns.Add("DateForReview", typeof(string));
            DocumentosTmp.Columns.Add("DateCreated", typeof(string));
            DocumentosTmp.Columns.Add("Reference", typeof(string));
            DocumentosTmp.Columns.Add("Author", typeof(string));
            DocumentosTmp.Columns.Add("DateReviewed", typeof(string));
            DocumentosTmp.Columns.Add("Scale", typeof(string));
            DocumentosTmp.Columns.Add("ToClientDate", typeof(string));
            DocumentosTmp.Columns.Add("Filename", typeof(string));
            DocumentosTmp.Columns.Add("FileSize", typeof(long));
            DocumentosTmp.Columns.Add("FileType", typeof(string));
            DocumentosTmp.Columns.Add("Confidential", typeof(bool));
            DocumentosTmp.Columns.Add("NoOfMarkups", typeof(int));
            DocumentosTmp.Columns.Add("RevisionDate", typeof(string));
            DocumentosTmp.Columns.Add("DateModified", typeof(string));
            DocumentosTmp.Columns.Add("PlannedSubmissionDate", typeof(string));
            DocumentosTmp.Columns.Add("MilestoneDate", typeof(string));
            DocumentosTmp.Columns.Add("ReviewStatus", typeof(string));
            DocumentosTmp.Columns.Add("ReviewSource", typeof(string));
            DocumentosTmp.Columns.Add("MarkupLastModifiedDate", typeof(string));
            DocumentosTmp.Columns.Add("ContractorDocumentNumber", typeof(string));
            DocumentosTmp.Columns.Add("AsBuiltRequired", typeof(bool));
            DocumentosTmp.Columns.Add("ContractDeliverable", typeof(bool));
            DocumentosTmp.Columns.Add("Check1", typeof(bool));
            DocumentosTmp.Columns.Add("Check2", typeof(bool));
            DocumentosTmp.Columns.Add("Category", typeof(string));
            DocumentosTmp.Columns.Add("Date1", typeof(string));
            DocumentosTmp.Columns.Add("Date2", typeof(string));
            DocumentosTmp.Columns.Add("VersionNumber", typeof(int));
            DocumentosTmp.Columns.Add("SelectList1", typeof(string));
            DocumentosTmp.Columns.Add("SelectList2", typeof(string));
            DocumentosTmp.Columns.Add("SelectList3", typeof(string));
            DocumentosTmp.Columns.Add("IsCurrent", typeof(bool));
            DocumentosTmp.Columns.Add("ContractNumber", typeof(string));

            if (_dbConDocs.State == ConnectionState.Closed)
                _dbConDocs.Open();

            _dbConDocs.Close();
        }


        private void dbcleartmptables()
        {
            // Si DocumentosTmp no es null, limpiar filas
            if (DocumentosTmp != null)
            {
                DocumentosTmp.Clear();
            }
        }

        private void datosactuales(string projectId)
        {
            try
            {
                var actualDB = new DataTable();

                if (_dbConDocs.State == ConnectionState.Closed)
                {
                    _dbConDocs.Open();  // ✅ usar la misma conexión
                }

                using (var da = new SqlDataAdapter(
                    "SELECT COUNT(*) AS total FROM Documentos WHERE [ACXProjectId] = @projid", _dbConDocs))
                {
                    da.SelectCommand.Parameters.AddWithValue("@projid", projectId);
                    da.Fill(actualDB);
                }

                if (actualDB.Rows.Count > 0)
                {
                    var totaldocs = actualDB.Rows[0]["total"].ToString();
                    Utilities.Wlog($"Documentos: Total de documentos antes del proceso {totaldocs}", 1);
                }
                else
                {
                    Utilities.Wlog("Documentos: Total de documentos antes del proceso (sin filas devueltas)", 1);
                }
            }
            catch (Exception ex)
            {
                Utilities.Wlog($"Documentos: ERROR {{datos actuales}}: {ex.Message}", 0);
            }
        }


        private async Task GetACXDocumentsAsync(string projectID, BackgroundWorker bgwdocs)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            var stopwatch = new System.Diagnostics.Stopwatch();

            try
            {
                string authcode = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(_config["ACXUser"] + ":" + _config["ACXPass"]));


                Utilities.Wlog($"Inicio GetACXDocuments para {projectID}", 1);
                stopwatch.Restart();
                await GetDocumentsAllAsync(projectID, authcode, bgwdocs);
                stopwatch.Stop();
                Utilities.Wlog($"[Documents] {DateTime.Now} Finalizó GetACXDocuments para {projectID} en {stopwatch.Elapsed.Minutes:D2}:{stopwatch.Elapsed.Seconds:D2} (mm:ss)", 1);
            }
            catch (Exception ex)
            {
                Utilities.Wlog($"Documentos: ERROR {{GetACXDocuments}}: {_config["NombrePrj"]} ({projectID}) Mensaje: {ex.Message}", 0);
            }
        }

        public void dbUpdateProjectData(string projid)
        {
            if (_dbConDocs.State == ConnectionState.Closed)
            {
                _dbConDocs.Open();
            }

            SqlTransaction transaction = null;

            try
            {
                if (DocumentosTmp.Rows.Count > 0)
                {
                    Utilities.Wlog($"Documentos: {DocumentosTmp.Rows.Count} documentos rescatados", 1);
                    AppState.totDoctosDescar = DocumentosTmp.Rows.Count;

                    using (SqlBulkCopy s = new SqlBulkCopy(_dbConDocs))
                    {
                        s.DestinationTableName = "Documentos_tmp";
                        s.ColumnMappings.Clear();

                        // Mapear cada columna por nombre para que no dependa del orden
                        foreach (DataColumn col in DocumentosTmp.Columns)
                        {
                            s.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                        }

                        s.WriteToServer(DocumentosTmp);
                        s.Close();
                    }

                    // Delete old data
                    transaction = _dbConDocs.BeginTransaction("BorraDocumentos");
                    using (SqlCommand sc = new SqlCommand($"DELETE Documentos WHERE ACXProjectId='{projid}'", _dbConDocs, transaction))
                    {
                        sc.ExecuteNonQuery();
                        transaction.Commit();
                    }

                    // Copy from tmp to final
                    transaction = _dbConDocs.BeginTransaction("CopiaDocumentos");
                    using (SqlCommand sc = new SqlCommand(@"
                    INSERT INTO [Documentos]
                    (
                        ACXProjectId, TrackingId, Id, DocumentNumber, Title, Revision, AuthorisedBy, DocumentType, 
                        DocumentStatus, Comments, Discipline, PrintSize, DateForReview, DateCreated, Reference, 
                        Author, DateReviewed, Scale, ToClientDate, Filename, FileSize, FileType, Confidential, 
                        NoOfMarkups, RevisionDate, DateModified, PlannedSubmissionDate, MilestoneDate, ReviewStatus, 
                        ReviewSource, MarkupLastModifiedDate, ContractorDocumentNumber, AsBuiltRequired, ContractDeliverable, 
                        Check1, Check2, Category, Date1, Date2, VersionNumber, SelectList1, SelectList2, SelectList3, IsCurrent, ContractNumber
                    )
                    SELECT 
                        ACXProjectId, TrackingId, Id, DocumentNumber, Title, Revision, AuthorisedBy, DocumentType, 
                        DocumentStatus, Comments, Discipline, PrintSize, DateForReview, DateCreated, Reference, 
                        Author, DateReviewed, Scale, ToClientDate, Filename, FileSize, FileType, Confidential, 
                        NoOfMarkups, RevisionDate, DateModified, PlannedSubmissionDate, MilestoneDate, ReviewStatus, 
                        ReviewSource, MarkupLastModifiedDate, ContractorDocumentNumber, AsBuiltRequired, ContractDeliverable, 
                        Check1, Check2, Category, Date1, Date2, VersionNumber, SelectList1, SelectList2, SelectList3, IsCurrent, ContractNumber
                    FROM Documentos_tmp
                    ", _dbConDocs, transaction))
                    {
                        sc.ExecuteNonQuery();
                        transaction.Commit();
                    }


                    // Truncate temp table
                    transaction = _dbConDocs.BeginTransaction("borradocumentostemp");
                    using (SqlCommand sc = new SqlCommand("TRUNCATE TABLE Documentos_tmp", _dbConDocs, transaction))
                    {
                        sc.ExecuteNonQuery();
                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.Wlog($"Documentos: ERROR {{dbUpdateProjectData}}: proyecto: {_config["NombrePrj"]}: {ex.Message}", 0);
            }

            _dbConDocs.Close();
        }

        private readonly object _lockDocTmp = new object();

        public async Task<bool> GetDocumentsAllAsync(string projid, string authcode, BackgroundWorker bgwdocs)
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            string uri = $"https://us1.aconex.com/api/projects/{projid}/register/search";

            var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);

            // 🔹 Configuración de headers (debe ir antes de la primera llamada)
            client.DefaultRequestHeaders.Add("Authorization", "Basic " + authcode);
            client.DefaultRequestHeaders.Add("X-Application-Key", "a7f7bf46-a848-4b7a-ae8c-ed55b3952010");

            try
            {
                // --- Obtener la primera página con reintento ---
                var firstPage = await Utilities.EjecutarConReintentosAsync(
                    () => GetPageAsync(client, uri, projid, 1),
                    $"Documentos: Error al obtener primera página del proyecto {projid}"
                );

                if (firstPage == null)
                    return false;

                int totalPages = firstPage.totalNumberOfPages;
                long allDocs = firstPage.totalResultsCount;
                long processedDocs = 0;

                bgwdocs.ReportProgress(0, $"Documentos: {allDocs} documentos en Aconex");
                bgwdocs.ReportProgress(0, $"Documentos: {totalPages} páginas");

                var semaphore = new SemaphoreSlim(5);
                var tasks = new List<Task>();

                // --- Bucle general (1..totalPages) ---
                for (int page = 1; page <= totalPages; page++)
                {
                    await semaphore.WaitAsync();
                    int currentPage = page;

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var pageData = currentPage == 1
                                ? firstPage
                                : await Utilities.EjecutarConReintentosAsync(
                                    () => GetPageAsync(client, uri, projid, currentPage),
                                    $"Documentos: Error al obtener página {currentPage} del proyecto {projid}"
                                  );

                            if (pageData != null)
                            {
                                lock (_lockDocTmp)
                                {
                                    foreach (var doc in pageData.searchResults)
                                    {
                                        try
                                        {
                                            AgregaDocumentoNuevo(doc, projid);
                                            processedDocs++;
                                        }
                                        catch (Exception ex)
                                        {
                                            Utilities.Wlog($"Documentos: ERROR al procesar doc {doc.Id} en proyecto {projid}, página {currentPage}: {ex.Message}", 0);
                                        }
                                    }
                                }

                                bgwdocs.ReportProgress(
                                    (int)(processedDocs * 100 / allDocs),
                                    $"Procesando {allDocs} documentos"
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            Utilities.Wlog($"Documentos: ERROR al obtener la página {currentPage} del proyecto {projid}: {ex.Message}", 0);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                AppState.TotDoctosAconex = processedDocs;
                return true;
            }
            catch (Exception ex)
            {
                Utilities.Wlog($"Documentos: ERROR general en proyecto {projid}: {ex.Message}", 0);
                return false;
            }
            finally
            {
                client.Dispose(); // 👈 aquí se libera siempre
            }
        }

        private async Task<Rootobject> GetPageAsync(HttpClient client, string uri, string projid, int page)
        {
            string[] returnFields = {
            "approved","asBuiltRequired","attribute1","attribute2","attribute3","attribute4",
            "author","authorisedBy","category","check1","check2","comments","comments2",
            "confidential","contractDeliverable","contractnumber","contractordocumentnumber",
            "contractorrev","current","date1","date2","discipline","docno","doctype","filename",
            "fileSize","fileType","forreview","markupLastModifiedDate","milestonedate",
            "numberOfMarkups","packagenumber","percentComplete","plannedsubmissiondate",
            "printSize","projectField1","projectField2","projectField3","received","reference",
            "registered","reviewed","reviewSource","reviewstatus","revision","revisiondate",
            "selectlist1","selectlist2","selectlist3","selectlist4","selectlist5","selectlist6",
            "selectlist7","selectlist8","selectlist9","selectlist10","scale","statusid",
            "tagNumber","title","toclient","trackingid","versionnumber","vdrcode",
            "vendordocumentnumber","vendorrev","versionnumber"
            };

            string orgid = _config["OrgId"];
            string userId = _config["userid"];

            var requestBody = new
            {
                orgId = orgid,
                userId = userId,
                returnFields = returnFields,
                resultSize = "300",
                showDocHistory = "true",
                pageNumber = page.ToString()
            };

            string postString = JsonConvert.SerializeObject(requestBody);

            var content = new StringContent(postString, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(uri, content);

            if (!response.IsSuccessStatusCode)
                return null;

            var responseString = await response.Content.ReadAsStringAsync();
            //responseString = responseString.Replace(((char)3).ToString(), "");
            responseString = responseString.Replace("\u0003", "");


            return JsonConvert.DeserializeObject<Rootobject>(responseString);
        }

        public void AgregaDocumentoNuevo(Searchresult mdoc, string projectId)
        {
            try
            {
                var row = DocumentosTmp.NewRow();

                // Campos principales
                row["Id"] = mdoc.Id;
                row["ACXProjectId"] = projectId;
                row["TrackingId"] = mdoc.TrackingId;
                row["DocumentNumber"] = mdoc.DocumentNumber;
                row["Title"] = mdoc.Title;
                row["Revision"] = mdoc.Revision;
                row["AuthorisedBy"] = mdoc.AuthorisedBy;
                row["DocumentType"] = mdoc.DocumentType;
                row["DocumentStatus"] = mdoc.DocumentStatus;
                row["Comments"] = mdoc.Comments;
                row["Discipline"] = mdoc.Discipline;
                row["PrintSize"] = mdoc.PrintSize;
                row["DateForReview"] = mdoc.DateForReview.ToString();
                row["DateCreated"] = mdoc.DateCreated.ToString();
                row["Reference"] = mdoc.Reference;
                row["Author"] = mdoc.Author;
                row["DateReviewed"] = mdoc.DateReviewed.ToString();
                row["Scale"] = mdoc.Scale;
                row["ToClientDate"] = mdoc.ToClientDate.ToString();
                row["Filename"] = mdoc.Filename;
                row["FileSize"] = Utilities.ParseLong(mdoc.FileSize);
                row["FileType"] = mdoc.FileType;
                row["Confidential"] = mdoc.Confidential;
                row["NoOfMarkups"] = mdoc.NoOfMarkups;
                row["RevisionDate"] = mdoc.RevisionDate.ToString();
                row["DateModified"] = mdoc.DateModified.ToString();
                row["PlannedSubmissionDate"] = mdoc.PlannedSubmissionDate.ToString();
                row["MilestoneDate"] = mdoc.MilestoneDate.ToString();
                row["ReviewStatus"] = mdoc.ReviewStatus;
                row["ReviewSource"] = mdoc.ReviewSource;
                row["MarkupLastModifiedDate"] = mdoc.MarkupLastModifiedDate.ToString();
                row["ContractorDocumentNumber"] = mdoc.ContractorDocumentNumber;
                //row["PackageNumber"] = JoinList(mdoc.PackageNumber);
                row["AsBuiltRequired"] = mdoc.AsBuiltRequired;
                row["ContractDeliverable"] = mdoc.ContractDeliverable;
                row["Check1"] = mdoc.Check1;
                row["Check2"] = mdoc.Check2;
                row["Category"] = mdoc.Category;
                row["Date1"] = mdoc.Date1.ToString();
                row["Date2"] = mdoc.Date2.ToString();
                row["VersionNumber"] = mdoc.VersionNumber;
                row["SelectList1"] = mdoc.SelectList1;
                row["SelectList2"] = mdoc.SelectList2;
                row["SelectList3"] = mdoc.SelectList3;
                row["IsCurrent"] = mdoc.IsCurrent;
                row["ContractNumber"] = (mdoc.ContractNumber != null && mdoc.ContractNumber.Length > 0)
                ? mdoc.ContractNumber[0] : "";
                DocumentosTmp.Rows.Add(row);
            }
            catch (Exception ex)
            {
                Utilities.Wlog($"Documentos: ERROR {{AgregaDocumentoNuevo}}:{projectId}:{ex.Message}", 0);
            }
        }
    }
}
