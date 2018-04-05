namespace Assets.Scripts
{
#if UNITY_EDITOR
  using UnityEditor;
#endif
  using UnityEngine;
  using UnityEngine.UI;
  public class MapViewController : MonoBehaviour
  {
    private bool paralleling = true;
    private Toggle parallelToggle;
 
    private Button findBtn;
    private Button randomizeBtn;
    private Button loadButton;
    private Button saveButton;
    private Text threadsStatusText;
    private Text pathInterestText;
    private Text pathTimeText;
    private Text timerText;
    private InputField SpawnRateIF;
    private InputField TRestrictionIF;

    private MapContent content;

    // Use this for initialization
    private void Start()
    {
      int restriction = int.Parse(GameObject.Find("TRestrictionInput").GetComponent<InputField>().text);
      var sprif = GameObject.Find("SpawnRateInput").GetComponent<InputField>();
      float spawnRate = float.Parse(sprif.text);
      content = new MapContent(spawnRate, restriction);
      Init();
    }
    private void Update()
    {
      timerText.text = content.SwCurrentTime;
      content.PrintBestPathIfNeeded();
    }

    public void OnTimeRestrictionChanged(string value)
    {
      if (content == null)
      {
        return;
      }
      int restriction;
      if (!int.TryParse(value, out restriction))
      {
        return;
      }
      if (content.TimeRestriction != restriction)
      {
        content.TimeRestriction = restriction;
        if (!content.Map.IsEmpty())
        {
          findBtn.interactable = true;
        }
      }
    }

    private void OnSpawnRateChanged(string value)
    {
      if (content?.Map == null)
      {
        return;
      }
      float spawnRate;
      value = value.Replace(".", ",");
      if (!float.TryParse(value, out spawnRate))
      {
        return;
      }
      if (spawnRate > 1)
      {
        spawnRate = 1;
      }
      if (spawnRate < 0)
      {
        spawnRate = 0;
      }
      if (Mathf.Abs(content.Map.SpawnRate - spawnRate) > 0.001)
      {
        content.Map.SpawnRate = spawnRate;
      }
    }

    /// <summary>
    /// called once upon application start
    /// </summary>
    private void Init()
    {
      threadsStatusText = GameObject.Find("ThreadsStatus").GetComponent<Text>();
      findBtn = GameObject.Find("FindBtn").GetComponent<Button>();
      randomizeBtn = GameObject.Find("RandomizeBtn").GetComponent<Button>();
      saveButton = GameObject.Find("SaveBtn").GetComponent<Button>();
      loadButton = GameObject.Find("LoadBtn").GetComponent<Button>();
      pathTimeText = GameObject.Find("PathTime").GetComponent<Text>();
      timerText = GameObject.Find("TimerText").GetComponent<Text>();
      pathInterestText = GameObject.Find("PathInterest").GetComponent<Text>();
      parallelToggle = GameObject.Find("ParallelToggle").GetComponent<Toggle>();
      SpawnRateIF = GameObject.Find("SpawnRateInput").GetComponent<InputField>();
      TRestrictionIF = GameObject.Find("TRestrictionInput").GetComponent<InputField>();

      SpawnRateIF.onValueChanged.AddListener(OnSpawnRateChanged);
      TRestrictionIF.onValueChanged.AddListener(OnTimeRestrictionChanged);
      parallelToggle.onValueChanged.AddListener(OnParallelToggle);
      findBtn.onClick.AddListener(OnFindBtnClick);
      randomizeBtn.onClick.AddListener(OnRandomizeButtonClick);
      saveButton.onClick.AddListener(OnSaveButtonClick);
      loadButton.onClick.AddListener(OnLoadButtonClick);

      InvokeRepeating("UpdatePathTime", 0.2f, 1f);
      InvokeRepeating("UpdatePathInterest", 0.2f, 1f);
      InvokeRepeating("UpdateThreadStatusText", 0.2f, 0.3f);

      OnParallelToggle(paralleling); //true by default 

      findBtn.interactable = false;
      saveButton.interactable = false;
    }

    private void OnLoadButtonClick()
    {
#if UNITY_EDITOR
      var path = EditorUtility.OpenFilePanel("Choose graph to load", "", "");
      GraphExporter.LoadGraph(content, path);
      findBtn.interactable = true;
#endif
    }

    private void OnSaveButtonClick()
    {
      GraphExporter.SaveGraph(content.Map);
      saveButton.interactable = false;
    }

    public void OnRandomizeButtonClick()
    {
      findBtn.interactable = true;
      content.OnRandomizeClick();
      saveButton.interactable = true;
    }

    public void OnFindBtnClick()
    {
      findBtn.interactable = false;
      content.FindPath(paralleling);
    }

    private void OnParallelToggle(bool value)
    {
      if (!content.IsMapEmpty)
      {
        findBtn.interactable = true;
      }
      paralleling = value;
    }

    private void UpdatePathTime()
    {
      pathTimeText.text = content.BestPathTime;
    }

    private void UpdatePathInterest()
    {
      pathInterestText.text = content.BestPathInterest;
    }

    private void UpdateThreadStatusText()
    {
      threadsStatusText.text = content.ThreadsСount.ToString();
    }
  }
}
