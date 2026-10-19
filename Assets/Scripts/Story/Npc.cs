using UnityEngine;

// Житель «Зари». По E открывает диалог из Story.json, во время разговора
// поворачивается к игроку, в остальное время стоит и изредка оглядывается.
public class Npc : Interactable
{
    [SerializeField] string npcName = "Житель";
    [Tooltip("Кто он: председатель, торговка… Показывается в окне диалога")]
    [SerializeField] string title = "";
    [Tooltip("id диалога в Assets/Resources/Data/Story.json")]
    [SerializeField] string dialogueId = "";

    Transform player;
    Quaternion idleRotation;
    float nextLookAround;

    public string NpcName => npcName;
    public string Title => title;
    public bool Talking { get; set; }

    public override string Prompt => $"Поговорить: {npcName}";
    public override bool CanInteract => !string.IsNullOrEmpty(dialogueId) && !Talking;

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        player = p != null ? p.transform : null;
        idleRotation = transform.rotation;
        nextLookAround = Time.time + Random.Range(3f, 8f);
    }

    public override void Interact(PlayerInteractor who)
    {
        if (DialogueUI.Instance != null)
            DialogueUI.Instance.Open(this, dialogueId);
    }

    void Update()
    {
        Quaternion target = idleRotation;
        if (player != null && (Talking || Vector3.Distance(player.position, transform.position) < 3f))
        {
            // Игрок подошёл — смотрим на него, как живой человек.
            Vector3 dir = player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                target = Quaternion.LookRotation(dir);
        }
        else if (Time.time >= nextLookAround)
        {
            idleRotation = Quaternion.Euler(0f, idleRotation.eulerAngles.y + Random.Range(-60f, 60f), 0f);
            nextLookAround = Time.time + Random.Range(4f, 10f);
        }
        // Во время разговора время стоит (timeScale = 0), поэтому поворот по реальному времени.
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 180f * Time.unscaledDeltaTime);
    }
}
