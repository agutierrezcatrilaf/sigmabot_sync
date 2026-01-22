using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigmabotSync.Domain.Models.Extraction
{
    public class Rootobject
    {
        public List<Searchresult> searchResults { get; set; }
        public int totalResultsCount { get; set; }
        public int totalResultsOnCurrentPage { get; set; }
        public int totalNumberOfPages { get; set; }
        public int currentPageNumber { get; set; }
        public int singlePageSize { get; set; }
    }

    public class Searchresult
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("documentNumber")] public string DocumentNumber { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("revision")] public string Revision { get; set; }
        [JsonProperty("approvalDate")] public string ApprovalDate { get; set; }
        [JsonProperty("authorisedBy")] public string AuthorisedBy { get; set; }
        [JsonProperty("documentType")] public string DocumentType { get; set; }
        [JsonProperty("documentStatus")] public string DocumentStatus { get; set; }
        [JsonProperty("comments")] public string Comments { get; set; }
        [JsonProperty("discipline")] public string Discipline { get; set; }
        [JsonProperty("printSize")] public string PrintSize { get; set; }
        [JsonProperty("dateForReview")] public string DateForReview { get; set; }
        [JsonProperty("dateCreated")] public string DateCreated { get; set; }
        [JsonProperty("reference")] public string Reference { get; set; }
        [JsonProperty("author")] public string Author { get; set; }
        [JsonProperty("dateReviewed")] public string DateReviewed { get; set; }
        [JsonProperty("scale")] public string Scale { get; set; }
        [JsonProperty("toClientDate")] public string ToClientDate { get; set; }
        [JsonProperty("filename")] public string Filename { get; set; }
        [JsonProperty("fileSize")] public string FileSize { get; set; }
        [JsonProperty("fileType")] public string FileType { get; set; }
        [JsonProperty("confidential")] public bool Confidential { get; set; }
        [JsonProperty("noOfMarkups")] public int NoOfMarkups { get; set; }
        [JsonProperty("revisionDate")] public string RevisionDate { get; set; }
        [JsonProperty("dateModified")] public string DateModified { get; set; }
        [JsonProperty("plannedSubmissionDate")] public string PlannedSubmissionDate { get; set; }
        [JsonProperty("milestoneDate")] public string MilestoneDate { get; set; }
        [JsonProperty("reviewStatus")] public string ReviewStatus { get; set; }
        [JsonProperty("reviewSource")] public string ReviewSource { get; set; }
        [JsonProperty("markupLastModifiedDate")] public string MarkupLastModifiedDate { get; set; }
        [JsonProperty("contractorDocumentNumber")] public string ContractorDocumentNumber { get; set; }
        [JsonProperty("asBuiltRequired")] public bool AsBuiltRequired { get; set; }
        [JsonProperty("contractDeliverable")] public bool ContractDeliverable { get; set; }
        [JsonProperty("check1")] public bool Check1 { get; set; }
        [JsonProperty("check2")] public bool Check2 { get; set; }
        [JsonProperty("category")] public string Category { get; set; }
        [JsonProperty("date1")] public string Date1 { get; set; }
        [JsonProperty("date2")] public string Date2 { get; set; }
        [JsonProperty("trackingid")] public long TrackingId { get; set; }
        [JsonProperty("versionNumber")] public int VersionNumber { get; set; }
        [JsonProperty("selectList1")] public string SelectList1 { get; set; }
        [JsonProperty("selectList2")] public string SelectList2 { get; set; }
        [JsonProperty("selectList3")] public string SelectList3 { get; set; }
        [JsonProperty("current")] public bool IsCurrent { get; set; }
        [JsonProperty("contractNumber")] public string[] ContractNumber { get; set; }
    }
}
