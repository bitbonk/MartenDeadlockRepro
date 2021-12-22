using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ReproApp.Wpf
{
    using System.Diagnostics;
    using System.Threading;
    using Marten;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += async (sender, args) =>
            {
                var store = DocumentStore.For("host=localhost;port=5433;database=repro;password=postgres;username=normal");

                await Read<Foo>(CancellationToken.None, store);
        
                // The following call never completes.
                // But reading the same table twice (call Read<Foo>() again) does not yield the problem.
                await Read<Bar>(CancellationToken.None, store);

                Debug.WriteLine("All was read!");
            };
        }
        
        private static async Task Read<T>(CancellationToken cancellationToken, DocumentStore store)
        {
            await using var session = store.QuerySession();
            var documents = await session
                .Query<T>()
                .ToListAsync(token: cancellationToken)
                .ConfigureAwait(false);
        }
        
        public class Foo
        {
            public int Id { get; set; }
            public string Text { get; set; }
        }

        public class Bar
        {
            public int Id { get; set; }
            public int Value { get; set; }
        }
    }
}