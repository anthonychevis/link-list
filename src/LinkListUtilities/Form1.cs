using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;



namespace Softtouch.LinkListUtilities
{
    public partial class Form1 : Form
    {

        const string TEMP_DIR = @"c:\temp";
        const string FROM_LABEL = "From <";
        const int MAX_FILES = 1000;
        enum ReadState { title, description, from };
        string _mostRecentJsonFile;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            label1.Text = "";
            label2.Text = "";
            label3.Text = "";

            DirectoryInfo rootDir = new DirectoryInfo(@"..\..\..\..\");
            textJsonDirectoryAndWildcard.Text = Path.Combine(rootDir.FullName, @"data\*.json");
            textMarkdownDirectory.Text = rootDir.FullName;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            label1.Text = "";
            var textContents = textBox1.Text; //Clipboard.GetText();
            List<LinkItem> linkList = ExtractDataFromText(new StringReader(textContents));

            string filename = "";

            for (int i = 0; i < MAX_FILES; i++) {
                string tryFilename = Path.Combine(TEMP_DIR, string.Format("LinkList{0:0000}.json", i));
                if (!File.Exists(tryFilename)) {
                    filename = tryFilename;
                    break;
                }
            }

            if (string.IsNullOrEmpty(filename)) {
                MessageBox.Show("You've reached the maximum of " + MAX_FILES + " files.");
                return;
            }

            Document document = new Document();
            document.LinkList = linkList;
            document.DocumentInfo = new DocumentInfo() {
                Heading = "Place heading here",
                PostedDate = DateTime.Now
            };

            string jsonDocument = JsonConvert.SerializeObject(document, Formatting.Indented);
            File.WriteAllText(filename, jsonDocument);
            label1.Text = $"{linkList.Count} records were written to {filename}";
            _mostRecentJsonFile = filename;
        }

        private List<LinkItem> ExtractDataFromText(StringReader reader)
        {
            List<LinkItem> outputList = new List<LinkItem>();
            ReadState state = ReadState.title;
            LinkItem record = new LinkItem();
            string line;
            record = new LinkItem();
            while ((line = reader.ReadLine()) != null) {

                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith(FROM_LABEL, StringComparison.InvariantCultureIgnoreCase)) {
                    state = ReadState.from;
                }

                var pair = ExtractAttribute(line);

                switch (pair.Item2.Name.ToLower()) {
                    case "tag":
                        record.Tag = pair.Item2.Value;
                        line = pair.Item1;
                        break;
                    case "author":
                        record.Author = pair.Item2.Value;
                        line = pair.Item1;
                        break;
                }
                
                switch (state) {
                    case ReadState.title:
                        if (line.Trim().Length == 0) break;
                        record.LinkPostedDate = dateTimePicker1.Value;
                        record.Title = line;
                        state = ReadState.description;
                        break;

                    case ReadState.description:
                        record.Description += line;
                        break;

                    case ReadState.from:
                        record.FromUrl = line.Substring(FROM_LABEL.Length - 1);
                        record.FromUrl.Replace("<", "").Replace(">", "").Replace(" ", "");
                        state = ReadState.title;
                        outputList.Add(record);
                        record = new LinkItem();
                        break;
                }
            }
            return outputList;
        }

        private Tuple<string, TextAttribute> ExtractAttribute(string text)
        {
            Tuple<string, TextAttribute> defaultResult = Tuple.Create(text, new TextAttribute("", ""));

            if (!text.StartsWith("{")) {
                return defaultResult;
            }

            int endPos = -1;
            int colonPos = -1;

            for (int i = 0; i < text.Length; i++) {
                if (text[i] == ':') {
                    colonPos = i;
                }
                if (text[i] == '}') {
                    endPos = i;
                    break;
                }
            }

            if (colonPos < 1 || endPos < 2) {
                return defaultResult;
            }

            string title = text.Substring(endPos + 1);
            string attribute = text.Substring(1, endPos - 1);
            string[] pair = attribute.Split(':');
            return Tuple.Create(title, new TextAttribute(pair[0], pair[1]));
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!File.Exists(_mostRecentJsonFile)) {
                return;
            }

            string sourceJsonFilename = _mostRecentJsonFile;
            string generateMarkdownFilename = sourceJsonFilename.Replace(".json", ".md"); ;

            Document document = JsonConvert.DeserializeObject<Document>(File.ReadAllText(sourceJsonFilename));

            RenderDocumentAsMarkdown(document, generateMarkdownFilename);

            label2.Text = $"{document.LinkList.Count} links were written to {generateMarkdownFilename}";
        }


        private void button3_Click(object sender, EventArgs e)
        {
            var sourcefiles = new DirectoryInfo(Path.GetDirectoryName(textJsonDirectoryAndWildcard.Text)).GetFiles(Path.GetFileName(textJsonDirectoryAndWildcard.Text));
            DirectoryInfo targetDirectory = new DirectoryInfo(textMarkdownDirectory.Text);

            BatchRenderDocumentAsMarkdown(sourcefiles, targetDirectory);
        }

        private void BatchRenderDocumentAsMarkdown(FileInfo[] jsonSourceFiles, DirectoryInfo outputDirectory)
        {

            int numberOfExistingOutputFiles = 0;

            foreach (var fileInfo in jsonSourceFiles) {
                if (outputDirectory.GetFiles(Path.GetFileNameWithoutExtension(fileInfo.Name) + ".md").Length > 0) {
                    numberOfExistingOutputFiles++;
                }
            }

            if (numberOfExistingOutputFiles > 0) {
                MessageBox.Show("There are " + numberOfExistingOutputFiles + " file(s) that would be overridden if this operation was to continue. Please manually delete the files and run this again.");
                return;
            }

            foreach (var sourceFileInfo in jsonSourceFiles) {
                Document document = JsonConvert.DeserializeObject<Document>(File.ReadAllText(sourceFileInfo.FullName));

                RenderDocumentAsMarkdown(document, Path.Combine(outputDirectory.FullName, Path.GetFileNameWithoutExtension(sourceFileInfo.Name) + ".md"));
            }

            textMarkdownDirectory.Text = "Finished processing " + jsonSourceFiles.Length + " file(s).";
        }

        private void RenderDocumentAsMarkdown(Document document, string generateMarkdownFilename)
        {
            if (File.Exists(generateMarkdownFilename)) {
                MessageBox.Show($"File already exists {generateMarkdownFilename}.");
                return;
            }

            using (StreamWriter writer = new StreamWriter(generateMarkdownFilename)) {

                writer.WriteLine($"# {document.DocumentInfo.Heading}");

                foreach (LinkItem item in document.LinkList) {
                    writer.WriteLine($"__{(!string.IsNullOrEmpty(item.Tag) ? "![tag](./tags/" + item.Tag + ".png)" : "")}{item.Title?.Trim()}__  ");
                    if (!string.IsNullOrEmpty(item.Author)) {
                        writer.WriteLine($"Author: {item.Author}  ");
                    }
                    if (!string.IsNullOrEmpty(item.Description)) {
                        writer.WriteLine($"{item.Description?.Trim() + "  "}");
                    }
                    writer.WriteLine($"<{item.FromUrl}>  ");
                    writer.WriteLine("***");
                }
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            var sourcefiles = new DirectoryInfo(Path.GetDirectoryName(textJsonDirectoryAndWildcard.Text)).GetFiles(Path.GetFileName(textJsonDirectoryAndWildcard.Text));

            foreach (var sourceFileInfo in sourcefiles) {

                Document document = JsonConvert.DeserializeObject<Document>(File.ReadAllText(sourceFileInfo.FullName));

                foreach (var linkItem in document.LinkList) {
                    linkItem.FromUrl = linkItem.FromUrl.Replace("<", "").Replace(">", "").Replace(" ", "");
                }

                string jsonDocument = JsonConvert.SerializeObject(document, Formatting.Indented);
                File.WriteAllText(sourceFileInfo.FullName, jsonDocument);

            }


        }

        private void button5_Click(object sender, EventArgs e)
        {

            var pastedData = GetPastedUrlAndTextContext();
            textBox1.Text = "URL: " + pastedData.Item1 + Environment.NewLine + "TEXT: " + pastedData.Item2;

            AutoInsertToLinkList(pastedData.Item1, pastedData.Item2);
        }

        private void AutoInsertToLinkList(string url, string textContent)
        {
            DateTime postedDate = dateTimePicker1.Value;

            string jsonDirectory = Path.GetDirectoryName(textJsonDirectoryAndWildcard.Text);
            FileInfo targetJsonFile = new FileInfo(Path.Combine(jsonDirectory, $"LinkList{postedDate.Year:0000}{postedDate.Month:00}"));

            Document document = null;
            if (targetJsonFile.Exists) {
                document = JsonConvert.DeserializeObject<Document>(File.ReadAllText(targetJsonFile.FullName));
            } else {
                document = new Document();
                document.DocumentInfo.Heading = $"Month of {postedDate.Year:0000}-{postedDate.Month:00}";
            }




            

            



        }

        private Tuple<string, string> GetPastedUrlAndTextContext()
        {
            string clipContent = Clipboard.GetText(TextDataFormat.Html);
            string textContent = Clipboard.GetText();

            string labelToFind = "SourceURL:";

            //int pos = content.IndexOf(labelToFind);
            //int afterLabel = pos + labelToFind.Length;

            StringReader reader = new StringReader(clipContent);
            string line;
            string url = "";

            while ((line = reader.ReadLine()) != null) {
                if (line.StartsWith(labelToFind)) {
                    url = line.Substring(labelToFind.Length);
                }
            }

            return Tuple.Create(url, textContent);
        }
    }
}

