using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace WSDLSorter
{
    public class WsdlSort
    {
        private readonly string _fileName;
        private readonly XmlDocument _xmlDocument;
        private readonly SortedSet<XmlNode> _sortedElements;
        private readonly XmlNamespaceManager _namespaceManager;

        public WsdlSort(string fileName)
        {
            _fileName = fileName;

            _xmlDocument = new XmlDocument();

            _namespaceManager = new XmlNamespaceManager(_xmlDocument.NameTable);
            _namespaceManager.AddNamespace("wsdl", @"http://schemas.xmlsoap.org/wsdl/");
            _namespaceManager.AddNamespace("xs", @"http://www.w3.org/2001/XMLSchema");

            _sortedElements = new SortedSet<XmlNode>(new XmlNodeComparer());
        }

        public void LoadXml()
        {
            _xmlDocument.Load(_fileName);
            SortDocument();
        }

        public void SaveSortedXml(string outFileName)
        {
            var xmlDoc = new XmlDocument();

            //var xmlDeclaration = xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
            var rootNode = xmlDoc.AppendChild(xmlDoc.CreateElement("root"));
            //xmlDoc.InsertBefore(xmlDeclaration, rootNode);

            foreach (var node in _sortedElements)
            {
                var importNode = xmlDoc.ImportNode(node, true);
                rootNode.AppendChild(importNode);
            }

            if (_sortedElements.Count != rootNode.ChildNodes.Count)
                throw new ArgumentException();

            var xmlAttributeCollections = xmlDoc.SelectNodes("//xs:element", _namespaceManager)?
                .Cast<XmlNode>()
                .Where(x => x.Attributes?["type"] != null)
                .Select(x => x.Attributes)
                ?? Enumerable.Empty<XmlAttributeCollection>();

            foreach (var attributes in xmlAttributeCollections)
            {
                var elementType = attributes["type"];

                var regex = new Regex("^q+\\d:");
                var match = regex.Match(elementType.Value);
                if (match.Success)
                {
                    var evilAttributeName = match.Groups[0].Captures[0].Value.Replace(":", string.Empty);
                    if (!string.IsNullOrEmpty(evilAttributeName))
                    {
                        var evilAttribute = attributes["xmlns:" + evilAttributeName];
                        if (evilAttribute != null)
                        {
                            attributes.Remove(evilAttribute);
                        }
                        attributes.Remove(elementType);
                    }
                }
            }

            xmlDoc.Save(outFileName);
        }

        private void SortDocument()
        {
            var schema = _xmlDocument.SelectSingleNode("wsdl:definitions/wsdl:types/xs:schema[contains(@targetNamespace,\"ilevelsolutions.com\")]",
                _namespaceManager);

            if (schema == null)
                throw new ArgumentException();

            foreach (var element in schema.ChildNodes)
            {
                _sortedElements.Add((XmlNode)element);
            }

            if (schema.ChildNodes.Count != _sortedElements.Count)
                throw new ArgumentException();
        }
    }

    public class XmlNodeComparer : IComparer<XmlNode>
    {
        public int Compare(XmlNode x, XmlNode y)
        {
            var xWeight = GetWeight(x);
            var yWeight = GetWeight(y);

            var dWeight = xWeight - yWeight;
            if (dWeight != 0)
                return dWeight;

            var attribute = x.LocalName == "import" ? "namespace" : "name";

            var xNameSpace = x.Attributes?[attribute]?.InnerText ?? string.Empty;
            var yNameSpace = y.Attributes?[attribute]?.InnerText ?? string.Empty;
            return string.Compare(xNameSpace, yNameSpace, StringComparison.InvariantCulture);
        }

        private int GetWeight(XmlNode node)
        {
            switch (node.LocalName)
            {
                case "import":
                    return 1;
                case "element":
                    return 2;
                case "simpleType":
                    return 3;
                case "complexType":
                    return 4;
                default:
                    return 10;
            }
        }
    }
}
