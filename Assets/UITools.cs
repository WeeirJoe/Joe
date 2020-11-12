using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;

 

public class UITools
{
public struct ExpText
{
public Text text;
public string path;
public int uid;
public ExpText(string path, Text text, int uid)
{
this.text = text;
this.path = path;
this.uid = uid;
}
}

private static string exportPath = Path.Combine(Application.dataPath, "exportLang.txt");
private static string subPath = "Resources/UI/Dialogs/";
private static string suff = "--end--";

[MenuItem("Assets/UI工具/导出UI文字", false, 4000)]
public static void CheckLang()
{ 
if (File.Exists(exportPath))
{
File.Delete(exportPath);
}

string path = Path.Combine(Application.dataPath, subPath);
List<string> files = new List<string>();
serchAllFiles(path, files);
List<ExpText> lst = new List<ExpText>();
for (int i = 0; i < files.Count; i++)
{
string assetPath = files[i].Replace(Application.dataPath, "Assets");
assetPath = assetPath.Replace(@"\", "/");
GameObject go = AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject)) as GameObject;
if (go != null)
{
getAllExpText(go.transform, ref lst);
}
}

for (int i = 0; i < lst.Count; i++)
{
//text文字不为空 不为单纯数字
if (lst[i].text != null && !string.IsNullOrEmpty(lst[i].text.text))
{
long num = -1;
if (!long.TryParse(lst[i].text.text, out num))
{
Write(exportPath, lst[i]);
}
}
}
AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
AssetDatabase.SaveAssets();
EditorUtility.DisplayDialog("导出完成", "导出所有预设Text文字完成,Txt目录Assets/exportLang.txt","OK");
}

private static void getAllExpText(Transform trans, ref List<ExpText> lst)
{
Text t = trans.GetComponent<Text>();
if (t != null)
{
string nodePath = getNodePath(trans, trans.name);
ExpText dt = new ExpText(nodePath, t, trans.gameObject.GetInstanceID());
lst.Add(dt);
}
for (int i = 0; i < trans.childCount; i++)
{
getAllExpText(trans.GetChild(i), ref lst);
}
}

private static string getNodePath(Transform trans, string path)
{
if (trans.parent != null)
{
path = trans.parent.name + "/" + path;
return getNodePath(trans.parent, path);
}
else
{
return path;
}
}

private static void serchAllFiles(string path, List<string> lst) {
string[] subPath = Directory.GetDirectories(path);
foreach (string item in subPath)
{
serchAllFiles(item, lst);
}
string[] files = Directory.GetFiles(path, "*.prefab");
lst.AddRange(files);
}


private static void Write(string path, ExpText exp)
{
FileStream fs = new FileStream(path, FileMode.Append);
StreamWriter sw = new StreamWriter(fs);
//开始写入 
StringBuilder builder = new StringBuilder();
builder.Append("\nuid~");
builder.Append(exp.uid.ToString());
builder.Append("|path~");
builder.Append(exp.path);
builder.Append("|text~");
string lang = exp.text.text;
lang = lang.Replace("|", "");
lang = lang.Replace("~", "");
builder.Append(exp.text.text);
builder.Append(suff);
//sw.Write("\nuid^" + exp.uid.ToString() + "|path^" + exp.path + "|text^" + exp.text.text + suff);
sw.Write(builder.ToString());
//清空缓冲区
sw.Flush();
//关闭流
sw.Close();
fs.Close();
}

[MenuItem("Assets/UI工具/导入UI文字", false, 4001)]
public static void ImportLang()
{
Dictionary<int, string> dict = getDict();
string path = Path.Combine(Application.dataPath, subPath);
List<string> files = new List<string>();
serchAllFiles(path, files);
for (int i = 0; i < files.Count; i++)
{
string assetPath = files[i].Replace(Application.dataPath, "Assets");
assetPath = assetPath.Replace(@"\", "/");
GameObject go = AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject)) as GameObject;
if (go != null)
{
replaceText(go.transform, dict);
}
}
AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
AssetDatabase.SaveAssets();
EditorUtility.DisplayDialog("导入完成", "导入所有预设Text文字完成,Txt目录Assets/exportLang.txt", "OK");
}

private static void replaceText(Transform trans, Dictionary<int, string> dict)
{
Text t = trans.GetComponent<Text>();
if (t != null)
{
int insId = trans.gameObject.GetInstanceID();
if (dict.ContainsKey(insId) && dict[insId] != t.text)
{
t.text = dict[insId];
EditorUtility.SetDirty(trans.gameObject);
}
}
for (int i = 0; i < trans.childCount; i++)
{
replaceText(trans.GetChild(i), dict);
}
}


private static Dictionary<int, string> getDict()
{
Dictionary<int, string> dict = new Dictionary<int, string>();
StreamReader sr = new StreamReader(exportPath, Encoding.UTF8);
String line;
string lastLine = "";
while ((line = sr.ReadLine()) != null)
{
if (lastLine.StartsWith("uid~") && lastLine.EndsWith(suff))
{
splitStr(lastLine, ref dict);
lastLine = "";
}
else
{
if (string.IsNullOrEmpty(lastLine))
{
lastLine = line;
}
else
{
lastLine = lastLine + @"\n" + line;
}
if (lastLine.StartsWith("uid~") && lastLine.EndsWith(suff))
{
splitStr(lastLine, ref dict);
lastLine = "";
}
}
}
return dict;
}

private static void splitStr(string line, ref Dictionary<int, string> dict)
{
string[] lst = line.Split('|');
int uid = -1;
string lang = "";
for (int i = 0; i < lst.Length; i++)
{
string str = lst[i];
string[] kv = str.Split('~');
if (kv[0] == "uid")
{
int.TryParse(kv[1], out uid);
}
else if (kv[0] == "text")
{
lang = kv[1];
}
}
if (uid != -1)
{
lang = lang.Replace(suff, "");
lang = System.Text.RegularExpressions.Regex.Unescape(lang);
dict.Add(uid, lang);
}
}

}