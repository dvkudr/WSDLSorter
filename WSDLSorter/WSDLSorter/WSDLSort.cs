using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace WSDLSorter
{
    public class WsdlSort
    {
        private readonly string _fileName;
        private readonly XmlDocument _xmlDocument;
        private readonly XmlNamespaceManager _namespaceManager;

        public WsdlSort(string fileName)
        {
            _fileName = fileName;

            _xmlDocument = new XmlDocument();

            _namespaceManager = new XmlNamespaceManager(_xmlDocument.NameTable);
            _namespaceManager.AddNamespace("wsdl", @"http://schemas.xmlsoap.org/wsdl/");
            _namespaceManager.AddNamespace("xs", @"http://www.w3.org/2001/XMLSchema");
            _namespaceManager.AddNamespace("soap", @"http://schemas.xmlsoap.org/wsdl/soap/");
            _namespaceManager.AddNamespace("soap12", @"http://schemas.xmlsoap.org/wsdl/soap12/");
        }

        public void LoadXml()
        {
            _xmlDocument.Load(_fileName);
        }

        public void SaveSchema()
        {
            var outFileName = AddPostfix("schema");

            var sortedElements = new SortedSet<XmlNode>(new XmlSchemaComparer());

            var schema = _xmlDocument.SelectSingleNode("wsdl:definitions/wsdl:types/xs:schema[contains(@targetNamespace,\"ilevelsolutions.com\")]",
                _namespaceManager);

            if (schema == null)
                throw new ArgumentException();

            foreach (var element in schema.ChildNodes)
            {
                sortedElements.Add((XmlNode)element);
            }

            if (schema.ChildNodes.Count != sortedElements.Count)
                throw new ArgumentException();

            var xmlDoc = new XmlDocument();

            var rootNode = xmlDoc.AppendChild(xmlDoc.CreateElement("root"));

            foreach (var node in sortedElements)
            {
                var importNode = xmlDoc.ImportNode(node, true);
                rootNode.AppendChild(importNode);
            }

            if (sortedElements.Count != rootNode.ChildNodes.Count)
                throw new ArgumentException();

            var elementNodes = xmlDoc.SelectNodes("//xs:element", _namespaceManager)?
                .Cast<XmlNode>()
                .Where(x => x.Attributes?["type"] != null)
                ?? Enumerable.Empty<XmlNode>();

            foreach (var elementNode in elementNodes)
            {
                if (elementNode.Attributes != null)
                {
                    var typeAttribute = elementNode.Attributes["type"];

                    var regex = new Regex("^q\\d+:");
                    var match = regex.Match(typeAttribute.Value);
                    if (match.Success)
                    {
                        var evilAttributeName = match.Groups[0].Captures[0].Value.Replace(":", string.Empty);
                        if (!string.IsNullOrEmpty(evilAttributeName))
                        {
                            var evilAttribute = elementNode.Attributes["xmlns:" + evilAttributeName];
                            if (evilAttribute != null)
                            {
                                var newAttribute = xmlDoc.CreateAttribute("r");
                                newAttribute.Value = evilAttribute.Value;
                                elementNode.Attributes.Append(newAttribute);
                                elementNode.Attributes.Remove(evilAttribute);
                            }
                            typeAttribute.Value = typeAttribute.Value.Replace(evilAttributeName, "r");
                        }
                    }
                }
            }

            xmlDoc.Save(outFileName);
        }

        public void SaveMessages()
        {
            SaveElementList(_xmlDocument, "wsdl:definitions/wsdl:message", "messages", null);
        }

        public void SaveOperations()
        {
            SaveElementList(_xmlDocument, "wsdl:definitions/wsdl:portType/wsdl:operation", "operations", x =>
            {
                foreach (var node in x.ChildNodes.Cast<XmlNode>())
                {
                    var actionAttribute = node.Attributes?["wsaw:Action"];
                    if (actionAttribute != null)
                    {
                        actionAttribute.Value = ReplaceQuarterId(actionAttribute.Value);
                    }
                }
            });
        }

        public void SaveBindings()
        {
            var bindings = _xmlDocument.SelectNodes("wsdl:definitions/wsdl:binding", _namespaceManager)?.Cast<XmlNode>().ToList();
            if (bindings == null)
                return;

            foreach (var binding in bindings)
            {
                var bindingName = binding.Attributes?["name"]?.Value;
                if (string.IsNullOrEmpty(bindingName))
                    continue;

                bindingName = bindingName.Replace("CustomBinding_", string.Empty);

                SaveElementList(binding, "wsdl:operation", bindingName, x =>
                {
                    var soapOperation = x.SelectSingleNode("soap:operation", _namespaceManager) ??
                                        x.SelectSingleNode("soap12:operation", _namespaceManager);

                    var soapAction = soapOperation?.Attributes?["soapAction"];
                    if (soapAction != null)
                    {
                        soapAction.Value = ReplaceQuarterId(soapAction.Value);
                    }
                });
            }
        }

        private void SaveElementList(XmlNode startNode, string xPathPattern, string outFileNamePostfix, Action<XmlNode> xmlNodeAdjustment)
        {
            var outFileName = AddPostfix(outFileNamePostfix);

            var sortedElements = new SortedSet<XmlNode>(new XmlNameComparer());

            var elements = startNode.SelectNodes(xPathPattern, _namespaceManager)?.Cast<XmlNode>().ToList();

            if (elements == null)
                return;

            foreach (var element in elements)
            {
                xmlNodeAdjustment?.Invoke(element);

                sortedElements.Add(element);
            }

            if (elements.Count != sortedElements.Count)
                throw new ArgumentException();

            var xmlDoc = new XmlDocument();

            var rootNode = xmlDoc.AppendChild(xmlDoc.CreateElement("root"));

            foreach (var node in sortedElements)
            {
                var importNode = xmlDoc.ImportNode(node, true);
                rootNode.AppendChild(importNode);
            }

            if (sortedElements.Count != rootNode.ChildNodes.Count)
                throw new ArgumentException();

            xmlDoc.Save(outFileName);
        }

        private string ReplaceQuarterId(string input)
        {
            var quarter = new Regex("/20\\d\\d/Q\\d");
            return quarter.Replace(input, "/20XX/QX");
        }

        private string AddPostfix(string postfix)
        {
            var directory = Path.GetDirectoryName(_fileName);
            var fileName = Path.GetFileNameWithoutExtension(_fileName);
            var extension = Path.GetExtension(_fileName);

            return $"{directory}{Path.DirectorySeparatorChar}{fileName}.{postfix}{extension}";
        }
    }

    public class XmlSchemaComparer : IComparer<XmlNode>
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

    public class XmlNameComparer : IComparer<XmlNode>
    {
        public int Compare(XmlNode x, XmlNode y)
        {
            var xname = x.Attributes?["name"]?.Value ?? string.Empty;
            var yname = y.Attributes?["name"]?.Value ?? string.Empty;

            return xname.CompareTo(yname);
        }
    }
}
