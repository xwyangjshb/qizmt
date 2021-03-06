﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MySpace.DataMining.DistributedObjects;


namespace RDBMS_DBCORE
{
    public partial class Qa
    {

        public class SysGenRemote : Remote
        {

            string DfsTableFile;
            string TableName;

            public override void OnRemote(IEnumerator<ByteSlice> input, System.IO.Stream output)
            {
                DfsTableFile = DSpace_ExecArgs[0];
                {
                    string RowInfo = Qa.QlArgsUnescape(DSpace_ExecArgs[1]);
                    string DisplayInfo = DSpace_ExecArgs[2];
                    //InitColInfos(RowInfo, DisplayInfo); // RDBMS_Select.DBCORE
                }
                TableName = DSpace_ExecArgs[3];

                if (0 == string.Compare(DfsTableFile, "qa://Sys.Tables", true))
                {
                    _SysTables(output);
                }
                else if (0 == string.Compare(DfsTableFile, "qa://Sys.TablesXML", true))
                {
                    _SysTablesXML(output);
                }
                else if (0 == string.Compare(DfsTableFile, "qa://Sys.Shell", true))
                {
                    _SysShell(output);
                }
                else if (0 == string.Compare(DfsTableFile, "qa://Sys.Help", true))
                {
                    _SysHelp(output);
                }
                else if (0 == string.Compare(DfsTableFile, "qa://Sys.Indexes", true))
                {
                    _SysIndexes(output);
                }
                else
                {
                    throw new Exception("Unknown system table file: " + DfsTableFile);
                }

            }


            void _SysShell(System.IO.Stream output)
            {
                string shellcmdraw;
                {
                    int ip = TableName.IndexOf('(');
                    if (-1 == ip || TableName[TableName.Length - 1] != ')')
                    {
                        return; // No command to run.
                    }
                    shellcmdraw = QlArgsUnescape(TableName.Substring(ip + 1, TableName.Length - ip - 1 - 1));
                }
                if (shellcmdraw.Length <= 2 || '\'' != shellcmdraw[0] || '\'' != shellcmdraw[shellcmdraw.Length - 1])
                {
                    return;
                }
                string shellcmd = shellcmdraw.Substring(1, shellcmdraw.Length - 2);
                string shelloutput = Shell(shellcmd);

                const int ColLineChars = 200;
                int ColLineLength = 1 + (ColLineChars * 2);
                string line = "";
                char endchar = '\n';
                List<byte> linebuf = new List<byte>();
                foreach (char chr in shelloutput)
                {
                    line += chr;
                    if (line.Length == ColLineChars || chr == endchar)
                    {
                        _OutputLine(line, output, ColLineLength, linebuf);
                        line = "";
                    }
                }

                if (line.Length > 0)
                {
                    _OutputLine(line, output, ColLineLength, linebuf);
                }
            }

            void _OutputLine(string line, System.IO.Stream output, int maxByteLength, List<byte> linebuf)
            {
                linebuf.Clear();
                StringToBytes(line, linebuf, maxByteLength);
                {
                    if (linebuf.Count > maxByteLength)
                    {
                        throw new Exception("Column Line: length is too long: " + line);
                    }
                    for (int ibs = 0; ibs < linebuf.Count; ibs++)
                    {
                        output.WriteByte(linebuf[ibs]);
                    }
                }
            }


            void _SysTablesXML(System.IO.Stream output)
            {
                string xml;
                using (GlobalCriticalSection.GetLock())
                {
                    xml = LoadSysTablesXml_unlocked();
                }

                int ColLineLength = 1 + (200 * 2);
                foreach (string _line in xml.Split('\n'))
                {
                    string line = _line.Trim('\n', '\r');
                    recordset rs = recordset.Prepare();
                    rs.PutByte(0); // Nullable byte.
                    rs.PutString(line);
                    {
                        ByteSlice bs = rs.ToByteSlice();
                        if (bs.Length > ColLineLength)
                        {
                            throw new Exception("Column Line: length is too long: " + line);
                        }
                        for (int ibs = 0; ibs < bs.Length; ibs++)
                        {
                            output.WriteByte(bs[ibs]);
                        }
                        for (int ibs = bs.Length; ibs < DSpace_OutputRecordLength; ibs++)
                        {
                            output.WriteByte(0);
                        }
                    }

                }

            }


            void _SysTables(System.IO.Stream output)
            {
                System.Xml.XmlDocument systables;
                using (GlobalCriticalSection.GetLock())
                {
                    systables = LoadSysTables_unlocked();
                }

                foreach (System.Xml.XmlNode xn in systables.SelectNodes("/tables/table"))
                {
                    recordset rs = recordset.Prepare();

                    int ColTableLength = 1 + (100 * 2);
                    string Table = xn["name"].InnerText;
                    rs.PutByte(0); // Nullable byte.
                    rs.PutString(Table);
                    {
                        ByteSlice bs = rs.ToByteSlice();
                        if (bs.Length > ColTableLength)
                        {
                            throw new Exception("Column Table: length is too long: " + Table);
                        }
                        for (int ibs = bs.Length; ibs < ColTableLength; ibs++)
                        {
                            rs.PutByte(0);
                        }
                    }

                    int ColFileLength = 1 + (120 * 2);
                    string File = xn["file"].InnerText;
                    rs.PutByte(0); // Nullable byte.
                    rs.PutString(File);
                    {
                        ByteSlice bs = rs.ToByteSlice();
                        if (bs.Length > ColTableLength + ColFileLength)
                        {
                            throw new Exception("Column File: length is too long: " + File);
                        }
                        for (int ibs = bs.Length; ibs < ColTableLength + ColFileLength; ibs++)
                        {
                            rs.PutByte(0);
                        }
                    }

                    {
                        ByteSlice bs = rs.ToByteSlice();
                        if (bs.Length > DSpace_OutputRecordLength)
                        {
                            throw new Exception("Record too long");
                        }
                        for (int ibs = 0; ibs < bs.Length; ibs++)
                        {
                            output.WriteByte(bs[ibs]);
                        }
                        for (int ibs = bs.Length; ibs < DSpace_OutputRecordLength; ibs++)
                        {
                            output.WriteByte(0);
                        }
                    }

                }

            }

            void _SysHelp(System.IO.Stream output)
            {
                string xml = LoadUsageXml();

                const int ColLineChars = 1000;
                int ColLineLength = 1 + (ColLineChars * 2);
                foreach (string _linex in xml.Split('\n'))
                {
                    string linex = _linex.Trim();
                    while (linex.Length > 0)
                    {
                        string line;
                        if (linex.Length > ColLineChars - 1)
                        {
                            line = linex.Substring(0, ColLineChars - 1);
                            linex = linex.Substring(ColLineChars - 1);
                        }
                        else
                        {
                            line = linex;
                            linex = "";
                        }
                        recordset rs = recordset.Prepare();
                        rs.PutByte(0); // Nullable byte.
                        rs.PutString(line);
                        {
                            ByteSlice bs = rs.ToByteSlice();
                            if (bs.Length > ColLineLength)
                            {
                                //DSpace_Log("bs.Length = " + bs.Length.ToString());
                                //DSpace_Log("ColLineLength = " + ColLineLength.ToString());
                                throw new Exception("Column Line: length is too long: " + line);
                            }
                            for (int ibs = 0; ibs < bs.Length; ibs++)
                            {
                                output.WriteByte(bs[ibs]);
                            }
                            for (int ibs = bs.Length; ibs < DSpace_OutputRecordLength; ibs++)
                            {
                                output.WriteByte(0);
                            }
                        }
                    }

                }
            }

            void _SysIndexes(System.IO.Stream output)
            {
                const int ColLineChars = 1000;
                int ColLineLength = 1 + (ColLineChars * 2);

                int ip = TableName.IndexOf('(');
                if (-1 == ip || TableName[TableName.Length - 1] != ')')
                {
                    return;
                }
                string indexdir = QlArgsUnescape(TableName.Substring(ip + 1, TableName.Length - ip - 1 - 1));
                System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo(indexdir);
                System.IO.FileInfo[] files = dir.GetFiles("ind.Index.*.ind");
                foreach (System.IO.FileInfo file in files)
                {
                    string[] parts = file.Name.Split('.');
                    string indexname = parts[2];

                    recordset rs = recordset.Prepare();
                    rs.PutByte(0); // Nullable byte.
                    rs.PutString(indexname);
                    {
                        ByteSlice bs = rs.ToByteSlice();
                        if (bs.Length > ColLineLength)
                        {
                            throw new Exception("Column Line: length is too long: " + indexname);
                        }
                        for (int ibs = 0; ibs < bs.Length; ibs++)
                        {
                            output.WriteByte(bs[ibs]);
                        }
                        for (int ibs = bs.Length; ibs < DSpace_OutputRecordLength; ibs++)
                        {
                            output.WriteByte(0);
                        }
                    }
                }
            }


            const string USAGE_FILENAME = "RDBMS_QA_Usage.xml";
            string LoadUsageXml()
            {
                string usagefp = IOUtils.GetTempDirectory() + @"\" + Guid.NewGuid() + USAGE_FILENAME;
                try
                {
                    Shell("dspace get " + USAGE_FILENAME + " \"" + usagefp + "\"");
                }
                catch (Exception e)
                {
                    if (-1 != e.Message.IndexOf("Error:  The specified file '" + USAGE_FILENAME + "' does not exist in DFS"))
                    {
                        throw new System.IO.FileNotFoundException("Unable to load " + USAGE_FILENAME + ": " + e.Message, e);
                    }
                    throw;
                }
                string result = System.IO.File.ReadAllText(usagefp);
                System.IO.File.Delete(usagefp);
                return result;
            }


            void StringToBytes(string str, List<byte> buf, int maxbyteCount)
            {
                byte[] strbytes = System.Text.Encoding.Unicode.GetBytes(str);
                buf.Add(0); //nullable byte
                buf.AddRange(strbytes);
                //Pad up the end of the char.
                while (buf.Count < maxbyteCount)
                {
                    buf.Add(0);
                }
            }


        }

    }

}