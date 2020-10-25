using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ModelView;
using System.Threading.Tasks;
using System;
using Avalonia.Threading;

namespace View
{
    class MyAppUIServices : UIServices
    {
        Window _window;
        ComboBox _classSelectorComboBox;
        ListBox _processedImagedListBox;
        ListBox _chosenClassListBox;
        TextBlock _processedImageTextBlock;
        TextBlock _chosenClassTextBlock;
        TextBlock _progressBarTextBlock;
        TextBlock _filterComboBoxTextBlock;
        ProgressBar _completionProgressBar;

        public MyAppUIServices(Window window, ComboBox classSelectorComboBox,
                               ListBox processedImagedListBox, ListBox chosenClassListBox,
                               TextBlock processedImageTextBlock, TextBlock chosenClassTextBlock,
                               TextBlock progressBarTextBlock, TextBlock filterComboBoxTextBlock,
                               ProgressBar completionProgressBar)
        {
            _window = window;
            _classSelectorComboBox = classSelectorComboBox;
            _processedImagedListBox = processedImagedListBox;
            _chosenClassListBox = chosenClassListBox;
            _filterComboBoxTextBlock = filterComboBoxTextBlock;
            _processedImageTextBlock = processedImageTextBlock;
            _chosenClassTextBlock = chosenClassTextBlock;
            _progressBarTextBlock = progressBarTextBlock;
            _completionProgressBar = completionProgressBar;
        }

        public async Task<string> ShowOpenDialogAsync() 
        {
            OpenFolderDialog OFD = new OpenFolderDialog();
            OFD.Directory = @"../..";
            OFD.Title = "Choose directory";
            
            string folder_name = await OFD.ShowAsync(_window);

            return folder_name;
        }

        //dont forget to add methods to hide or visualize
        public void IsVisibleProgressBar(bool value)
        {
            _progressBarTextBlock.IsVisible = value;
            _completionProgressBar.IsVisible = value;
        }

        public void IsVisibleProcessedImageViewer(bool value) 
        {
            _processedImagedListBox.IsVisible = value;
            _processedImageTextBlock.IsVisible = value;
        }

        public void IsVisibleFilteredImageViewer(bool value) 
        {
            _chosenClassListBox.IsVisible = value;
            _chosenClassTextBlock.IsVisible = value;
        }

        public void IsVisibleClassFilter(bool value) 
        {
            _classSelectorComboBox.IsVisible = value;
            _filterComboBoxTextBlock.IsVisible = value;
        }
    }

    public class MainWindow : Window
    {
        ModelView.ModelView mv;
        MyAppUIServices app_uiservices;

        public MainWindow()
        {
            InitializeComponent();

            app_uiservices = new MyAppUIServices(this,
                                                 this.FindControl<ComboBox>("classSelectorComboBox"),
                                                 this.FindControl<ListBox>("processedImagedListBox"),
                                                 this.FindControl<ListBox>("chosenClassListBox"),
                                                 this.FindControl<TextBlock>("processedImageTextBlock"),
                                                 this.FindControl<TextBlock>("chosenClassTextBlock"),
                                                 this.FindControl<TextBlock>("progressBarTextBlock"),
                                                 this.FindControl<TextBlock>("filterComboBoxTextBlock"),
                                                 this.FindControl<ProgressBar>("completionProgressBar"));
            mv = new ModelView.ModelView(app_uiservices);

            DataContext = mv;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}