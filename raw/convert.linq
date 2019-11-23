<Query Kind="Program" />

Func<int, string>[] MASTER_FUNCS = new[]
{
	(Func<int, string>)(i => i>=1  && i<=26  ? "Book_1.lyx" : null),
	(Func<int, string>)(i => i>=27 && i<=54  ? "Book_2.lyx" : null),
	(Func<int, string>)(i => i>=55 && i<=82  ? "Book_3.lyx" : null),
	(Func<int, string>)(i => i>=83 && i<=999 ? "Book_4.lyx" : null),
};

string cqp = Path.GetDirectoryName(Util.CurrentQueryPath);

void Main()
{
	var inputFiles = Directory.EnumerateFiles(Path.Combine(cqp, "epub")).Where(p => p.ToLower().EndsWith(".html")).ToList();
	
	foreach (var f in inputFiles)
	{
		var input = File.ReadAllText(f, Encoding.UTF8);
		
		var output = Process(input, out var outfilename);
		
		File.WriteAllText(Path.Combine(cqp, "latex", outfilename), output, Encoding.UTF8);
	}
}

string Process(string html, out string filename)
{
	var template = File.ReadAllText(Path.Combine(cqp, "tex_template.txt"), Encoding.UTF8);
	
	html = html.Trim();
	
	AssertRemoveFirstLine(ref html, "<!DOCTYPE html>");
	AssertRemoveFirstLine(ref html, "<html>");
	AssertRemoveFirstLine(ref html, "<body>");
	AssertRemoveLastLine(ref html, "</html>");
	AssertRemoveLastLine(ref html, "</body>");

	RemoveFirstLine(ref html, out var title);

	if (!title.StartsWith("<h1>")) throw new Exception($"Assertion failed :: No <h1>");
	if (!title.EndsWith("</h1>")) throw new Exception($"Assertion failed :: No </h1>");

	title = title.Substring("<h1>".Length, title.Length - "<h1>".Length - "</h1>".Length);
	int chapnum = int.Parse(title.Split(' ')[0].TrimEnd('.'));
	title = title.Substring(title.Split(' ')[0].Length+1).TrimStart();

	filename = $"chap_{chapnum:000}.lyx";

	if (html.StartsWith("<p> </p>")) RemoveFirstLine(ref html, out _);
	if (html.StartsWith("<p></p>"))  RemoveFirstLine(ref html, out _);

	RemoveFirstLine(ref html, out var _chunk);
	if (!_chunk.StartsWith("<p style=\"text-align: center\">")) throw new Exception($"Assertion failed :: No chapter p");

	var master = MASTER_FUNCS.Single(p => p(chapnum) != null)(chapnum);

	template = template.Replace("{{master}}", master);
	template = template.Replace("{{title}}", title);
	template = template.Replace("{{now}}", $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}");

	var bc = new StringBuilder();
	foreach (var _line in html.Split('\n'))
	{
		var line = _line.Trim();
		
		if (line == @"<p style=""text-align: center"">* * *</p>")
		{
			// TODO ----BREAK----

			bc.AppendLine("\\begin_layout Standard");
			bc.AppendLine("- break -");
			bc.AppendLine("\\end_layout");
			bc.AppendLine("");

			continue;
		}

		line = line.Replace("&nbsp;", " ");

		var center = false;
		
		if (line.StartsWith(@"<p style=""text-align: center"">"))
		{
			line = "<p>" + line.Substring(@"<p style=""text-align: center"">".Length);
			center = true;
		}
		
		if (!line.StartsWith("<p>")) throw new Exception($"Assertion failed :: No <p>");
		if (!line.EndsWith("</p>")) throw new Exception($"Assertion failed :: No </p>");
		line = line.Substring("<p>".Length, line.Length - "<p>".Length - "</p>".Length);

		line = line.Replace("<em>", "\n\\emph on\n");
		line = line.Replace("</em>", "\n\\emph default\n");

		line = line.Replace("<strong>", "\n\\series bold\n");
		line = line.Replace("</strong>", "\n\\series default\n");

		line = line.Replace("<sup>", "\n\\begin_inset script superscript\\begin_layout Plain Layout\n");
		line = line.Replace("</sup>", "\n\\end_layout\\end_inset\n");

		line = line.Replace("<br>", "\n\\begin_inset Newline newline\\end_inset\n");

		line = line.Replace("“", "\"");
		line = line.Replace("”", "\"");
		line = line.Replace("’", "'");
		line = line.Replace("‘", "'");

		if (line.Contains("<")) throw new Exception($"Assertion failed :: No tags");
		if (line.Contains(">")) throw new Exception($"Assertion failed :: No tags");
		
		line = ColumnSplit(line, 80);

		bc.AppendLine("\\begin_layout Standard");
		if (center) bc.AppendLine("\\align block");
		bc.AppendLine(line);
		bc.AppendLine("\\end_layout");
		bc.AppendLine("");
	}

	template = template.Replace("{{content}}", bc.ToString());

	return template;
}

void AssertRemoveFirstLine(ref string data, string assertion)
{
	int idx = data.IndexOf('\n');
	if (idx < 0) throw new Exception($"Assertion failed :: No line-1");

	var line1 = data.Substring(0, idx).Trim();

	data = data.Substring(idx + 1).Trim();

	if (line1 != assertion) throw new Exception($"Assertion failed :: [{line1}] <> [{assertion}]");
}

void AssertRemoveLastLine(ref string data, string assertion)
{
	int idx = data.LastIndexOf('\n');
	if (idx < 0) throw new Exception($"Assertion failed :: No line-1");

	var lineE = data.Substring(idx+1).Trim();

	data = data.Substring(0, idx).Trim();

	if (lineE != assertion) throw new Exception($"Assertion failed :: [{lineE}] <> [{assertion}]");
}

void RemoveFirstLine(ref string data, out string line1)
{
	int idx = data.IndexOf('\n');
	if (idx < 0) throw new Exception($"Assertion failed :: No line-1");
	
	line1 = data.Substring(0, idx).Trim();

	data = data.Substring(idx + 1).Trim();
}

string ColumnSplit(string text, int len)
{
	text = text.Replace("\r\n", "\n");

	var output = new StringBuilder();

	int cc = 0;

	var start = 0;
	var last_space = -1;
	var last_char = '\0';
	for (int i = 0; i < text.Length; i++)
	{
		if (i>0)last_char=text[i-1];
		var chr = text[i];
		cc++;

		if (chr == '\n')
		{
			output.AppendLine(text.Substring(start, i - start + 1));
			start = i + 1;
			cc = 0;
			last_space = -1;
			continue;
		}
		if (chr == ' ') last_space = i;

		if (cc >= 80 && last_space != -1)
		{
			output.AppendLine(text.Substring(start, last_space - start));
			start = last_space;
			cc = i - start + 1;
			last_space = -1;
			continue;
		}

		if (chr == ' ' && last_char == '.')
		{
			output.AppendLine(text.Substring(start, last_space - start));
			start = last_space;
			cc = i - start + 1;
			last_space = -1;
			continue;
		}
	}

	if (start < text.Length)
	{
		output.AppendLine(text.Substring(start));
	}

	return output.ToString();
}







