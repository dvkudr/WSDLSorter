using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace WSDLSorter
{
    public class WsdlSort
    {
        private readonly string _fileName;
        private readonly XmlDocument _xmlDocument;

        public WsdlSort(string fileName)
        {
            _fileName = fileName;
            _xmlDocument = new XmlDocument();
        }

        public void LoadXml()
        {
            _xmlDocument.Load(_fileName);
            SortDocument();
        }

        public void SaveSortedXml(string outFileName)
        {
            _xmlDocument.Save(outFileName);
        }

        private void SortDocument()
        {
            // TODO sort xml
        }
    }
}
