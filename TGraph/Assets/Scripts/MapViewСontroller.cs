using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace Assets.Scripts
{
  public class MapViewСontroller : MonoBehaviour
  {
    private bool paralleling = true;
    private Toggle parallelToggle;
    public float SpawnRate;
    public int TimeRestriction;
 
    private Text threadsStatusText;
    private Text pathInterestText;
    private Text pathTimeText;
    private Text timerText;

    private Button findBtn;
    private Button randomizeBtn;
    private MapContent content;

    // Use this for initialization
    private void Start()
    {
      content = new MapContent(SpawnRate, TimeRestriction);
      Init();
    }

    public void OnRandomizeButtonClick()
    {
      findBtn.interactable = true;
      content.OnRandomizeClick();
    }

    public void OnFindBtnClick()
    {
      findBtn.interactable = false;
      content.FindPath(paralleling);
    }

    private void OnParallelToggle(bool value)
    {
      if (content.Map.Any())
      {
        findBtn.interactable = true;
      }
      paralleling = value;
    }

    /// <summary>
    /// called once upon application start
    /// </summary>
    private void Init()
    {
      threadsStatusText = GameObject.Find("ThreadsStatus").GetComponent<Text>();
      findBtn = GameObject.Find("FindBtn").GetComponent<Button>();
      randomizeBtn = GameObject.Find("RandomizeBtn").GetComponent<Button>();
      pathTimeText = GameObject.Find("PathTime").GetComponent<Text>();
      timerText = GameObject.Find("TimerText").GetComponent<Text>();
      pathInterestText = GameObject.Find("PathInterest").GetComponent<Text>();
      parallelToggle = GameObject.Find("ParallelToggle").GetComponent<Toggle>();

      parallelToggle.onValueChanged.AddListener(OnParallelToggle);
      findBtn.onClick.AddListener(OnFindBtnClick);
      randomizeBtn.onClick.AddListener(OnRandomizeButtonClick);

      InvokeRepeating("UpdatePathTime", 0.2f, 1f);
      InvokeRepeating("UpdatePathInterest", 0.2f, 1f);
      InvokeRepeating("UpdateThreadStatusText", 0.2f, 1f);
     
      OnParallelToggle(paralleling); //true by default 

      findBtn.interactable = false;
    }

    private void Update()
    {
      timerText.text = content.SwCurrentTime;

      content.PrintBestPathIfNeeded();


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
      threadsStatusText.text = content.StateTokensCount;
    }

    private void OnApplicationQuit()
    {
      Debug.Log("Quit apllication");
      content.ClearThreads();
    }
  }
}
