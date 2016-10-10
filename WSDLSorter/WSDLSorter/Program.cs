using System;
using System.IO;

namespace WSDLSorter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
                return;

            var wsdlName = args[0];

            var wsdlSort = new WsdlSort(wsdlName);

            wsdlSort.LoadXml();
            wsdlSort.SaveSchema();
            wsdlSort.SaveMessages();
            wsdlSort.SaveOperations();
            wsdlSort.SaveBindings();
        }
    }
}
