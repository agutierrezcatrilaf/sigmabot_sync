using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegrationWorkers.Models.Document
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
}
