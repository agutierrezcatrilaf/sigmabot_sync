using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegrationWorkers.Models
{
    public class Issues
    {
        public List<Issue> issues { get; set; }
        public int results_on_page { get; set; }
        public int page_size { get; set; }
        public int page_number { get; set; }
        public int total_results { get; set; }
        public int total_pages { get; set; }
    }

    public class Issue
    {
        public string description { get; set; }
        public string status { get; set; }
        public dynamic attachments { get; set; }           // Puede ser List<object> o dynamic
        public dynamic area { get; set; }                  // Necesario para area("name")
        public dynamic project { get; set; }
        public List<string> permissions { get; set; }
        public string issue_id { get; set; }
        public string issue_number { get; set; }
        public string location_detail { get; set; }
        public string area_sort_string { get; set; }
        public dynamic issue_type { get; set; }            // issue_type("name")
        public dynamic assigned_to { get; set; }           // assigned_to("user")("first_name") etc.
        public dynamic assigned_by { get; set; }
        public string assigned_at { get; set; }
        public string due_date { get; set; }
        public List<CustomField> custom_fields { get; set; }
        public dynamic meta_data { get; set; }             // meta_data("created_at")
        public dynamic closed_by { get; set; }             // closed_by("user")("first_name") etc.
        public string closed_at { get; set; }
        public string event_aggregation_id { get; set; }
        public dynamic issue_source { get; set; }
        public dynamic issue_listings { get; set; }
        public dynamic attachment { get; set; }
        public dynamic conversations { get; set; }
    }

    public class CustomField
    {
        public string id { get; set; }
        public string value { get; set; }
        public string family_id { get; set; }
    }

}
