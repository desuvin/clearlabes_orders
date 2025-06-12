using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ShopifySharp;
using ShopifySharp.Filters;
using System.Configuration;
using System.IO;
using System.Xml;

namespace clearlabes_orders
{
    class Program
    {
        private static string path = ConfigurationManager.AppSettings["path"];
        private static string path_backup = ConfigurationManager.AppSettings["path_backup"];
        private static string log_path = ConfigurationManager.AppSettings["log_path"];

        private static DateTime customdate = DateTime.Now.Date.AddDays(-5);
        private static DateTime currentDateTime = DateTime.Now.Date;

        private static string shopUrl = "clearlab-spain.myshopify.com";
        private static string accessToken = Environment.GetEnvironmentVariable("B2B_ES");

        static async Task Main(string[] args)
        {
            //Console.WriteLine(accessToken);
            Console.WriteLine("Loading Settings ...");
            Console.WriteLine("Custom Date : " + customdate.ToString("yyyy-MM-dd"));
            Console.WriteLine("Current Date : " + currentDateTime.ToString("yyyy-MM-dd"));

            Console.WriteLine("Loading Configuration ...");

            Console.WriteLine("Current Filepath : " + path);

            Console.WriteLine("Connecting to Shopify Store");
            var shopService = new ShopService(shopUrl, accessToken);
            Console.WriteLine("Succesfully Connected to : " + shopService);

            Console.WriteLine("Getting Orders from the store ...");
            var orderService = new OrderService(shopUrl, accessToken);

            var filter = new OrderListFilter()
            {
                Limit = 250,
                Status = "any",
                CreatedAtMin = customdate,
                FinancialStatus = "pending",
                FulfillmentStatus = "unfulfilled"
            };

            var ordercount = 0;
            while (true)
            {
                var orders = await orderService.ListAsync(filter);
                var ordercountpercall = 0;
                foreach (var orderitem in orders.Items)
                {
                    if (orderitem.CancelledAt != null) { continue; }
                    Console.WriteLine("--------------------------------------------------");


                    Console.WriteLine("Creating XML...");
                    int checkcount = checkFiles(orderitem.OrderNumber.ToString());
                    if (checkcount > 0) { continue; }
                    await createXML(orderitem, path, shopUrl, accessToken);

                    ordercountpercall++;
                }

                ordercount += ordercountpercall;
                if (ordercountpercall < 250)
                {
                    break;
                }
            }

            Console.WriteLine("Total Orders : " + ordercount);
        }

        private static async Task createXML(Order orderItem, string path, string shopUrl, string accessToken)
        {
            //List<string> lines = new List<string>();
            List<string> logs = new List<string>();
            if (orderItem is Order order)
            {
                XmlDocument doc = new XmlDocument();
                XmlDeclaration xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
                XmlElement root = doc.DocumentElement;
                doc.InsertBefore(xmlDeclaration, root);
                XmlElement elemB2bOrder = doc.CreateElement(string.Empty, "B2BOrder", string.Empty);
                doc.AppendChild(elemB2bOrder);

                XmlElement elemOrderType = doc.CreateElement(string.Empty, "OrderType", string.Empty);
                XmlText textOrderType = doc.CreateTextNode("B2B Order");

                elemOrderType.AppendChild(textOrderType);
                elemB2bOrder.AppendChild(elemOrderType);

                XmlElement elemOrderNo = doc.CreateElement(string.Empty, "OrderNo", string.Empty);
                XmlText textOrderNo = doc.CreateTextNode("B2B " + order.OrderNumber.ToString());
                elemOrderNo.AppendChild(textOrderNo);
                elemB2bOrder.AppendChild(elemOrderNo);

                string requestedDeliveryDateText;
                string orderDateText;
                if (order.ProcessedAt.HasValue)
                {
                    requestedDeliveryDateText = order.ProcessedAt.Value.ToString("dd/MM/yyyy");
                    orderDateText = order.ProcessedAt.Value.ToString("dd/MM/yyyy");
                }
                else
                {
                    requestedDeliveryDateText = "N/A";
                    orderDateText = "N/A";
                }

                XmlElement elemOrderDate = doc.CreateElement(string.Empty, "OrderDate", string.Empty);
                XmlText textOrderDate = doc.CreateTextNode(orderDateText);
                elemOrderDate.AppendChild(textOrderDate);
                elemB2bOrder.AppendChild(elemOrderDate);

                //customer code
                string customerCode = string.Empty;
                string shiptToCode = string.Empty;
                if (order.Customer != null)
                {
                    var metafieldService = new MetaFieldService(shopUrl, accessToken);
                    var metafields = await metafieldService.ListAsync(order.Customer.Id.Value, "customers");

                    var customerCodeMetafield = metafields.Items.FirstOrDefault(m => m.Namespace == "custom" && m.Key == "navision_account");
                    if (customerCodeMetafield != null)
                    {
                        customerCode = customerCodeMetafield.Value?.ToString();
                    }

                    var customerShipToCodeMetafield = metafields.Items.FirstOrDefault(m => m.Namespace == "custom" && m.Key == "ship_to_code");
                    if (customerShipToCodeMetafield != null)
                    {
                        shiptToCode = customerShipToCodeMetafield.Value?.ToString();
                    }
                }


                XmlElement elemCustomerCode = doc.CreateElement(string.Empty, "CustomerCode", string.Empty);
                XmlText textCustomerCode = doc.CreateTextNode(customerCode ?? string.Empty);
                elemCustomerCode.AppendChild(textCustomerCode);
                elemB2bOrder.AppendChild(elemCustomerCode);

                string customerName = string.Empty;
                if (orderItem.Customer != null)
                {
                    customerName = orderItem.Customer.FirstName + " " + orderItem.Customer.LastName;
                }


                XmlElement elemCustomerName = doc.CreateElement(string.Empty, "CustomerName", string.Empty);
                XmlText textCustomerName = doc.CreateTextNode(customerName);
                elemCustomerName.AppendChild(textCustomerName);
                elemB2bOrder.AppendChild(elemCustomerName);

                XmlElement elemCurrency = doc.CreateElement(string.Empty, "Currency", string.Empty);
                XmlText textCurrency = doc.CreateTextNode(order.Currency.ToString());
                elemCurrency.AppendChild(textCurrency);
                elemB2bOrder.AppendChild(elemCurrency);

                XmlElement elemShipName = doc.CreateElement(string.Empty, "ShipToCode", string.Empty);
                XmlText textShipName = doc.CreateTextNode(shiptToCode);
                elemShipName.AppendChild(textShipName);
                elemB2bOrder.AppendChild(elemShipName);

                XmlElement elemRequestedDeliveryDate = doc.CreateElement(string.Empty, "RequestedDeliveryDate", string.Empty);
                XmlText textRequestedDeliveryDate = doc.CreateTextNode(requestedDeliveryDateText);
                elemRequestedDeliveryDate.AppendChild(textRequestedDeliveryDate);
                elemB2bOrder.AppendChild(elemRequestedDeliveryDate);

                XmlElement elemExternalDocumentNo = doc.CreateElement(string.Empty, "ExternalDocumentNo", string.Empty);
                XmlText textExternalDocumentNo = doc.CreateTextNode("B2B #" + order.OrderNumber.ToString());
                elemExternalDocumentNo.AppendChild(textExternalDocumentNo);
                elemB2bOrder.AppendChild(elemExternalDocumentNo);

                XmlElement elemOrderRemarks = doc.CreateElement(string.Empty, "OrderRemarks", string.Empty);
                string orderNote = order?.Note;
                XmlText textOrderRemarks = doc.CreateTextNode(orderNote);
                elemOrderRemarks.AppendChild(textOrderRemarks);
                elemB2bOrder.AppendChild(elemOrderRemarks);

                XmlElement elemDeliveryRemarks = doc.CreateElement(string.Empty, "DeliveryRemarks", string.Empty);
                string deliveryRemarks = order?.Note;
                XmlText textDeliveryRemarks = doc.CreateTextNode(deliveryRemarks);
                elemDeliveryRemarks.AppendChild(textDeliveryRemarks);
                elemB2bOrder.AppendChild(elemDeliveryRemarks);

                Console.WriteLine("Order ID : " + orderItem.Id);
                Console.WriteLine("Order Name : " + orderItem.Name);
                Console.WriteLine("Order Created At : " + orderItem.CreatedAt);
                Console.WriteLine("Order Updated At : " + orderItem.UpdatedAt);
                Console.WriteLine("Order Financial Status : " + orderItem.FinancialStatus);
                Console.WriteLine("Order Fulfillment Status : " + orderItem.FulfillmentStatus);
                Console.WriteLine("Order Total Price : " + orderItem.TotalPrice);
                Console.WriteLine("Order Currency : " + orderItem.Currency);
                Console.WriteLine("Order Line Items Count : " + orderItem.LineItems.Count());
                Console.WriteLine("Order Customer Code : " + customerCode);
                Console.WriteLine("Order Customer Name : " + customerName);
                Console.WriteLine("Order Customer ShipTo Code : " + shiptToCode);
                Console.WriteLine("Order Requested Delivery Date : " + requestedDeliveryDateText);
                Console.WriteLine("Order External Document No : " + "Shopify #" + order.OrderNumber.ToString());
                Console.WriteLine("Order Note : " + orderNote);

                Console.WriteLine("Order Line Items : ");

                int LineNumber = 1000;
                foreach (var item in order.LineItems)
                {
                    XmlElement elemOrderItems = doc.CreateElement(string.Empty, "OrderItems", string.Empty);

                    XmlElement elemOrderLineNumber = doc.CreateElement(string.Empty, "OrderLineNumber", string.Empty);
                    XmlText textOrderLineNumber = doc.CreateTextNode(LineNumber.ToString());
                    elemOrderLineNumber.AppendChild(textOrderLineNumber);
                    elemOrderItems.AppendChild(elemOrderLineNumber);

                    XmlElement elemType = doc.CreateElement(string.Empty, "Type", string.Empty);
                    XmlText textType = doc.CreateTextNode("1");
                    elemType.AppendChild(textType);
                    elemOrderItems.AppendChild(elemType);

                    XmlElement elemItemCode = doc.CreateElement(string.Empty, "ItemCode", string.Empty);
                    elemOrderItems.AppendChild(elemItemCode);

                    XmlElement elemItemDescription = doc.CreateElement(string.Empty, "ItemDescription", string.Empty);
                    elemOrderItems.AppendChild(elemItemDescription);

                    XmlElement elemVariantCode = doc.CreateElement(string.Empty, "VariantCode", string.Empty);
                    elemOrderItems.AppendChild(elemVariantCode);

                    XmlElement elemUOM = doc.CreateElement(string.Empty, "UOM", string.Empty);
                    elemOrderItems.AppendChild(elemUOM);

                    XmlElement elemItemCrossReference = doc.CreateElement(string.Empty, "ItemCrossReference", string.Empty);
                    XmlText textItemCrossReference = doc.CreateTextNode(item.SKU);
                    elemItemCrossReference.AppendChild(textItemCrossReference);
                    elemOrderItems.AppendChild(elemItemCrossReference);

                    XmlElement elemOrderQty = doc.CreateElement(string.Empty, "OrderQty", string.Empty);
                    XmlText textOrderQty = doc.CreateTextNode(item.Quantity.ToString());
                    elemOrderQty.AppendChild(textOrderQty);
                    elemOrderItems.AppendChild(elemOrderQty);

                    XmlElement elemUnitPrice = doc.CreateElement(string.Empty, "UnitPrice", string.Empty);
                    elemOrderItems.AppendChild(elemUnitPrice);

                    string patientReference = string.Empty;
                    foreach (var property in item.Properties)
                    {
                        if (property.Name.ToString() == "Patient Reference")
                        {
                            Console.WriteLine("Property Name : " + property.Name);
                            Console.WriteLine("Property Value : " + property.Value);
                            patientReference = property.Value.ToString();
                        }
                        
                    }

                    XmlElement elemPatientReference = doc.CreateElement(string.Empty, "PatientReference", string.Empty);
                    XmlText textPatientReference = doc.CreateTextNode(patientReference);
                    elemPatientReference.AppendChild(textPatientReference);
                    elemOrderItems.AppendChild(elemPatientReference);


                    XmlElement elemOrderLineRemarks = doc.CreateElement(string.Empty, "OrderLineRemarks", string.Empty);
                    elemOrderItems.AppendChild(elemOrderLineRemarks);

                    XmlElement elemUseNavPrice = doc.CreateElement(string.Empty, "UseNAVPrice", string.Empty);
                    XmlText textUseNavPrice = doc.CreateTextNode("Yes");
                    elemUseNavPrice.AppendChild(textUseNavPrice);
                    elemOrderItems.AppendChild(elemUseNavPrice);

                    XmlElement elemUseCrossRef = doc.CreateElement(string.Empty, "UseCrossRef", string.Empty);
                    XmlText textUseCrossRef = doc.CreateTextNode("Yes");
                    elemUseCrossRef.AppendChild(textUseCrossRef);
                    elemOrderItems.AppendChild(elemUseCrossRef);

                    elemB2bOrder.AppendChild(elemOrderItems);
                    LineNumber += 1000;
                }

                doc.Save(path + "Shopify_" + order.OrderNumber.ToString() + ".xml");
                var confirmMessage = "xml " + order.OrderNumber.ToString() + " has been generated";
                Console.WriteLine(confirmMessage);
                logs.Add(confirmMessage);

            }
            else
            {
                Console.WriteLine("Invalid order type.");
                logs.Add("Error on Creating order");
            }

            string filename = "log-shopify-clearlabes-" + DateTime.Now.ToString("yyyyMMdd") + ".log";
            string logFilePath = ConfigurationManager.AppSettings["log_path"] + filename;

            LogListContents(logs, logFilePath);
        }

        public static void LogListContents(List<string> loglist, string logFilePath)
        {
            try
            {
                using (StreamWriter sw = File.AppendText(logFilePath))
                {
                    foreach (string logEntry in loglist)
                    {
                        sw.WriteLine(logEntry);
                    }
                }

                Console.WriteLine("Log file created at: " + logFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing to the log file: " + ex.Message);
            }
        }

        private static int checkFiles(string ordernumber)
        {
            string filepath = path_backup + "\\" + "Shopify_" + ordernumber + ".xml";
            Console.WriteLine("Checking File : " + filepath);
            if (File.Exists(filepath))
            {
                Console.WriteLine("File Exists : " + filepath);
                return 1;
            }
            else
            {
                Console.WriteLine("File Not Found : " + filepath);
                return 0;
            }
        }

    }
}
