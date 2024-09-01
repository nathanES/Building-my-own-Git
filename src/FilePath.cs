namespace codecrafters_git;

public static class FilePath
{
#if DEBUG
   public const string PATH_TO_GIT_FOLDER = "../../../.git/";
#else
   public const string PATH_TO_GIT_FOLDER = ".git/";
#endif

   public const string TO_GIT_OBJECTS_FOLDER = PATH_TO_GIT_FOLDER + "objects/";
}