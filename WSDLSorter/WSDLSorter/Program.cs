using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WSDLSorter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
                return;

            var wsdlSort = new WsdlSort(args[0]);

            wsdlSort.LoadXml();
            wsdlSort.SaveSortedXml(args[1]);
        }
    }
}
