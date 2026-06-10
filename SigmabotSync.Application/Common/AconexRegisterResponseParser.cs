using System.Xml;

namespace SigmabotSync.Application.Common
{
    public static class AconexRegisterResponseParser
    {
        public static string ParseRegisterDocumentId(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return null;
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(responseText);
                XmlNode node = doc.SelectSingleNode("//RegisterDocumentResult")
                    ?? doc.SelectSingleNode("/*[local-name()='RegisterDocumentResult']");
                return node?.InnerText?.Trim();
            }
            catch
            {
                return null;
            }
        }
    }
}
