using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Softtouch.LinkListUtilities
{
    public class LinkItem
    {
        public DateTime LinkPostedDate { get; set; } = DateTime.Now;
        public string Title { get; set; }
        public string Description { get; set; }
        public string FromUrl { get; set; }
        public string Tag { get; set; }
    }

    public class DocumentInfo
    {
        public DateTime PostedDate { get; set; } = DateTime.Now;
        public string Heading { get; set; }
        public string SourceFilename { get; set; }
    }

    public class Document
    {
        public DocumentInfo DocumentInfo;
        public List<LinkItem> LinkList;
    }
}
