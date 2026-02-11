using Markdig.Extensions.Tables;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Markup.Renderers
{
    internal class TableRenderer : ObjectRenderer<Table>
    {
        private const string k_TableStyleBase = "mui-chat-response-table";
        private const string k_TableStyleCell = k_TableStyleBase + "-cell-style";
        private const string k_TableStyleHeader = k_TableStyleBase + "-header-style";
        private const string k_TableStyleRowEven = k_TableStyleBase + "-even-row-cell-style";
        private const string k_TableStyleRowOdd = k_TableStyleBase + "-odd-row-cell-style";
        private const string k_TableStyleCellCenter = k_TableStyleBase + "-cell-text-alignment-center";
        private const string k_TableStyleCellRight = k_TableStyleBase + "-cell-text-alignment-right";

        protected override void Write(ChatMarkdownRenderer renderer, Table table)
        {
            var oldNewLine = renderer.m_ParagraphAddNewLine;
            renderer.m_ParagraphAddNewLine = false;

            int cols = 0;
            int rows = 0;
            foreach (var rowObj in table)
            {
                rows++;

                var row = (TableRow)rowObj;
                if (row.Count > cols)
                    cols = row.Count;
            }

            var tableElement = renderer.StartNewTableElement();
            tableElement.SetDimensions(cols, rows);

            int rowIdx = 0;
            foreach (var rowObj in table)
            {
                var row = (TableRow)rowObj;

                var tableElementRow = tableElement.GetRow(rowIdx);

                tableElementRow.style.marginTop = 1;

                if (rowIdx == rows - 1)
                    tableElementRow.style.marginBottom = 1;

                bool hasHeader = false;
                for (int i = 0; i < row.Count; i++)
                {
                    var cellObj = row[i];
                    var cell = (TableCell)cellObj;

                    var textElement = renderer.GetCurrentTextElement(tableElementRow);

                    renderer.Write(cell);

                    textElement.style.marginBottom = 0;

                    textElement.AddToClassList(k_TableStyleCell);

                    if (i == 0)
                    {
                        textElement.style.marginLeft = 1;
                    }

                    if (row.IsHeader)
                    {
                        textElement.AddToClassList(k_TableStyleHeader);
                        hasHeader = true;
                    }
                    else if (hasHeader && rowIdx % 2 == 0 || !hasHeader && rowIdx % 2 == 1)
                        textElement.AddToClassList(k_TableStyleRowEven);
                    else
                        textElement.AddToClassList(k_TableStyleRowOdd);

                    var alignment = table.ColumnDefinitions[i].Alignment;
                    if (alignment == TableColumnAlign.Center)
                    {
                        textElement.AddToClassList(k_TableStyleCellCenter);
                    }
                    else if (alignment == TableColumnAlign.Right)
                    {
                        textElement.AddToClassList(k_TableStyleCellRight);
                    }

                    renderer.CloseTextElement();

                    tableElementRow.Add(textElement);
                }

                rowIdx++;
            }

            renderer.AppendText("\n");

            renderer.m_ParagraphAddNewLine = oldNewLine;
        }
    }
}
