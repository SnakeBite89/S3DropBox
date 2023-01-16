using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using S3DropBox.Service;
using System.Configuration.Install;
using System.Reflection;

namespace S3DropBox
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(/*string[] args*/)
        {
            /*if (Environment.UserInteractive)
            {
                var parameter = string.Concat(args);
                switch (parameter)
                {
                    case "--install":
                        ManagedInstallerClass.InstallHelper(new[] { Assembly.GetExecutingAssembly().Location });
                        break;
                    case "--uninstall":
                        ManagedInstallerClass.InstallHelper(new[] { "/u", Assembly.GetExecutingAssembly().Location });
                        break;
                }
                Console.WriteLine("we are installing");
            }
            else
            {*/
                Console.WriteLine("we are starting");
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                new S3SyncService()
                };
                ServiceBase.Run(ServicesToRun);
            //}           
        }
    }
}
