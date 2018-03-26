

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

    private MapContent content;

    public int TimeRestriction;
    public float SpawnRate;

    // Use this for initialization
    private void Start()
    {
      content = new MapContent(SpawnRate, TimeRestriction);
      Init();
    }
    private void Update()
    {
      timerText.text = content.SwCurrentTime;
      content.PrintBestPathIfNeeded();
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
      var path = EditorUtility.OpenFilePanel("Choose graph to load", "", "*.*");
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
