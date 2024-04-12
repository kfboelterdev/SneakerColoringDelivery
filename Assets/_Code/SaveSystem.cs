using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public class SaveSystem : MonoSingleton<SaveSystem> {

    [SerializeField] private string _imageFolder;
    private string _imagePath;
    [SerializeField] private string _baseImageFolder;
    [HideInInspector] public string baseImagePath;
    [SerializeField] private string _saveFolder;
    private string _savePath;
    [SerializeField] private string _workedImageFolder;
    private string _workedImagePath;

    [Header("Cache")]

    private FileStream _fileStreamCache;
    private BinaryFormatter _binaryFormatterCache = new BinaryFormatter();

#if UNITY_EDITOR
    [Header("Debug")]

    [SerializeField] private bool _debugLogs;
#endif
    protected override void Awake() {
        base.Awake();

        _imagePath = $"{Application.persistentDataPath}/{_imageFolder}";
        baseImagePath = $"{Application.persistentDataPath}/{_baseImageFolder}";
        _savePath = $"{Application.persistentDataPath}/{_saveFolder}";
        _workedImagePath = $"{Application.persistentDataPath}/{_workedImageFolder}";
        if (!Directory.Exists(_imagePath)) Directory.CreateDirectory(_imagePath);
        if (!Directory.Exists(baseImagePath)) Directory.CreateDirectory(baseImagePath);
        if (!Directory.Exists(_savePath)) Directory.CreateDirectory(_savePath);
        if (!Directory.Exists(_workedImagePath)) Directory.CreateDirectory(_workedImagePath);
    }

    public void Save(string imageName, Stack<ColorAction> actionsPerformed, Texture2D textureToSave) {
#if UNITY_EDITOR
        if (_debugLogs) {
            if (actionsPerformed.Count > 0) Debug.Log("Game Saved");
            else {
                Debug.LogWarning("There are no actions to be saved");
                return;
            }
        }
#endif
        WriteSave(actionsPerformed, imageName);
        WriteImageColored(textureToSave, imageName);
        WriteWorkedImage(textureToSave, imageName);
    }

    private void WriteSave(Stack<ColorAction> save, string imageName) {
        string savePath = $"{_savePath}/{imageName}.cbs"; // CBS = ColoringBookSave
        _fileStreamCache = File.Exists(savePath) ? File.Open(savePath, FileMode.Open) : File.Create(savePath);
        _binaryFormatterCache.Serialize(_fileStreamCache, save);
        _fileStreamCache.Close();
    }

    public Stack<ColorAction> ReadSave(string imageName) {
        string savePath = $"{_savePath}/{imageName}.cbs"; // CBS = ColoringBookSave
        Stack<ColorAction> saveData = null;
        if (File.Exists(savePath)) {
            _fileStreamCache = File.Open(savePath, FileMode.Open);
            saveData = (Stack<ColorAction>)_binaryFormatterCache.Deserialize(_fileStreamCache);
            _fileStreamCache.Close();
        }
#if UNITY_EDITOR
        if (_debugLogs && saveData == null) Debug.Log("No Save was found");
#endif

        return saveData;
    }

    public List<WorkedImage> GetAllWorkedImages() {
        List<WorkedImage> workedImages = new List<WorkedImage>();
        foreach (FileInfo file in new DirectoryInfo(_workedImagePath).GetFiles()) workedImages.Add(ReadWorkedImage(file.Name));
        return workedImages;
    }

    private void WriteWorkedImage(Texture2D textureToSave, string imageName) {
        string workedPath = $"{_workedImagePath}/{imageName}.cbw"; // CBW = ColoringBookWI
        _fileStreamCache = File.Exists(workedPath) ? File.Open(workedPath, FileMode.Open) : File.Create(workedPath);

        WorkedImage workedImage = MyWorkUpdater.Instance.GetWorkedImageByName(imageName);
        if (workedImage.name == null) workedImage = new WorkedImage(imageName);

        MyWorkUpdater.Instance.UpdateWorkedImageList(workedImage, textureToSave);
        ImageListUpdater.Instance.UpdateSelectionTexture(imageName, textureToSave, imageName.Split("-").Length > 1 ? int.Parse(imageName.Split("-")[1]) : 0);

        _binaryFormatterCache.Serialize(_fileStreamCache, workedImage);
        _fileStreamCache.Close();
    }

    private WorkedImage ReadWorkedImage(string imageName) {
        string imagePath = $"{_workedImagePath}/{imageName}";
        WorkedImage workedImage;
        _fileStreamCache = File.Open(imagePath, FileMode.Open);
        workedImage = (WorkedImage)_binaryFormatterCache.Deserialize(_fileStreamCache);
        _fileStreamCache.Close();
        return workedImage;
    }

    public void WriteImageBase(Texture2D imageBase, string imageName) {
        string imagePath = $"{baseImagePath}/{imageName}.cbib"; // CBIB = ColoringBookImageBase
        File.WriteAllBytes(imagePath, imageBase.EncodeToPNG());
    }

    public Texture2D ReadImageBase(string imageName) {
        string imagePath = $"{baseImagePath}/{imageName}.cbib";
        if (!File.Exists(imagePath)) return null;

        Texture2D tex = new Texture2D(0, 0);
        tex.LoadImage(File.ReadAllBytes(imagePath));

        return tex;
    }

    private void WriteImageColored(Texture2D imageColored, string imageName) {
        string imagePath = $"{_imagePath}/{imageName}.cbt";
        File.WriteAllBytes(imagePath, imageColored.EncodeToPNG());
    }

    public Texture2D ReadImageColored(string imageName, bool checkForDuplicates = false) {
        string imagePath = $"{_imagePath}/{imageName}.cbt";
        if (!File.Exists(imagePath)) {
            if (checkForDuplicates) {
                bool exists = false;
                foreach (FileInfo file in new DirectoryInfo(_imagePath).GetFiles()) {
                    if (file.Name.Contains(imageName)) {
                        imagePath = $"{_imagePath}/{file.Name}";
                        Debug.Log(imagePath);
                        exists = true;
                        break;
                    }
                }
                if (!exists) return null;
            }
            else return null;
        }
        Texture2D tex = new Texture2D(0, 0);
        tex.LoadImage(File.ReadAllBytes(imagePath));

        return tex;
    }

    public void WriteJsonVersion(ImageInfoJson json) {
        File.WriteAllText($"{Application.persistentDataPath}/versionInfo.cbj", JsonUtility.ToJson(json)); // CBJ = ColoringBookJson
    }

    public ImageInfoJson ReadLocalJsonVersion() {
        return File.Exists($"{Application.persistentDataPath}/versionInfo.cbj") ? JsonUtility.FromJson<ImageInfoJson>(File.ReadAllText($"{Application.persistentDataPath}/versionInfo.cbj")) : new ImageInfoJson(true);
    }

    public bool CheckJsonExistence() {
        return File.Exists($"{Application.persistentDataPath}/versionInfo.cbj");
    }

    public void DeleteSave(string imageName) {
        File.Delete($"{_savePath}/{imageName}.cbs");
        File.Delete($"{_imagePath}/{imageName}.cbt");
        File.Delete($"{_workedImagePath}/{imageName}.cbw");
    }

    public Texture2D CheckSaveStateReturnTexToUse(string imageName) {
        Texture2D tex = CheckWorkedImageAmountWithName(imageName) > 0 ? ReadImageColored(imageName, true) : ReadImageBase(imageName);
        if (tex == null) Debug.Log("There was no Image to be used!");

        return tex;
    }

    public int CheckWorkedImageAmountWithName(string name) {
        int i = 0;
        foreach (FileInfo file in new DirectoryInfo(_workedImagePath).GetFiles()) if (file.Name.Contains(name)) i++;
        return i;
    }

    // Debug
    public void DeleteAllSaves() {
        Debug.Log(new DirectoryInfo(Application.persistentDataPath).GetFiles().Length);
        foreach (FileInfo file in new DirectoryInfo(_savePath).GetFiles()) file.Delete();
        foreach (FileInfo file in new DirectoryInfo(baseImagePath).GetFiles()) file.Delete();
        foreach (FileInfo file in new DirectoryInfo(_imagePath).GetFiles()) file.Delete();
        foreach (FileInfo file in new DirectoryInfo(_workedImagePath).GetFiles()) file.Delete();
        File.Delete($"{Application.persistentDataPath}/versionInfo.cbj");

        Application.Quit();
    }
}