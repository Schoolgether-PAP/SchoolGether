using System.Resources;
namespace SchoolGether.Web.Learning;
internal static class ClassroomMessages
{
 static readonly ResourceManager Resources=new("SchoolGether.Web.Learning.ClassroomMessages",typeof(ClassroomMessages).Assembly);
 internal static string Get(string key)=>Resources.GetString(key)??key;
}
