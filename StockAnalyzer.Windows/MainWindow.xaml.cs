using Newtonsoft.Json;
using StockAnalyzer.Core;
using StockAnalyzer.Core.Domain;
using StockAnalyzer.Core.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace StockAnalyzer.Windows
{
    public partial class MainWindow : Window
    {
        private static string API_URL = "https://ps-async.fekberg.com/api/stocks";
        private Stopwatch stopwatch = new Stopwatch();

        public MainWindow()
        {
            InitializeComponent();
        }



        CancellationTokenSource cancellationTokenSource;

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            if (cancellationTokenSource != null)
            {
                // Already have an instance of the cancellation token source?
                // This means the button has already been pressed!

                cancellationTokenSource.Cancel();
                cancellationTokenSource = null;

                Search.Content = "Search";
                return;
            }

            try
            {
                cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.Token.Register(() =>
                {
                    Notes.Text = "Cancellation requested";
                });
                Search.Content = "Cancel"; // Button text

                BeforeLoadingStockData();

                var identifiers = StockIdentifier.Text.Split(',', ' ');

                //Here, you can either consume the StockService or the MockStockService
                //var service = new StockService();
                var service = new MockStockService();

                var loadingTasks = new List<Task<IEnumerable<StockPrice>>>();

                foreach (var identifier in identifiers)
                {
                    //This queries the API
                    var loadTask = service.GetStockPricesFor(identifier, cancellationTokenSource.Token);

                    //This captures each task, which is part of an ongoing asynchronous operation
                    loadingTasks.Add(loadTask);
                }
                //If the search takes longer than 120 seconds, the operation should be cancelled
                var timeoutTask = Task.Delay(120000);

                var allStocksLoadingTask = Task.WhenAll(loadingTasks);

                //Which of the 2 tasks (timeoutTask, allStocksLoadingTask) was completed first?
                var completedTask = await Task.WhenAny(timeoutTask, allStocksLoadingTask);

                if (completedTask == timeoutTask)
                {
                    cancellationTokenSource.Cancel();
                    throw new OperationCanceledException("Timeout!");
                }

                //To make sure all of the tasks are completed before updating the UI
                //It will then populate to a flat list of all the stock prices then populate the UI
                //WhenAll is an asychronous operation because it's a task, that needs the "await"
                //var allStocks = await Task.WhenAll(loadingTasks);

                //The SelectMany takes all of the lists of stock prices and puts them into one flat list
                Stocks.ItemsSource = allStocksLoadingTask
                    .Result
                    .SelectMany(x => x);
            }
            catch (Exception ex)
            {
                Notes.Text = ex.Message;
            }
            finally
            {
                AfterLoadingStockData();

                cancellationTokenSource = null;
                Search.Content = "Search";
            }
        }









        private static Task<List<string>>
            SearchForStocks(CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                using (var stream = new StreamReader(File.OpenRead("StockPrices_Small.csv")))
                {
                    var lines = new List<string>();

                    string line;
                    while ((line = await stream.ReadLineAsync()) != null)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        lines.Add(line);
                    }

                    return lines;
                }
            }, cancellationToken);
        }

        private async Task GetStocks()
        {
            try
            {
                var store = new DataStore();

                var responseTask = store.GetStockPrices(StockIdentifier.Text);

                Stocks.ItemsSource = await responseTask;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


















        private void BeforeLoadingStockData()
        {
            stopwatch.Restart();
            StockProgress.Visibility = Visibility.Visible;
            StockProgress.IsIndeterminate = true;
        }

        private void AfterLoadingStockData()
        {
            StocksStatus.Text = $"Loaded stocks for {StockIdentifier.Text} in {stopwatch.ElapsedMilliseconds}ms";
            StockProgress.Visibility = Visibility.Hidden;
        }

        private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));

            e.Handled = true;
        }

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
