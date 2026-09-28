using UnityEngine;

// Un punto de limpieza: el montón de basura de una misión (una esquina, el
// estacionamiento del Plaza Vea, la orilla del río...). MissionDirector
// prende UNO por zona y el vecino te dice exactamente dónde queda.
public class CleanupSpot : MonoBehaviour
{
    [SerializeField] private string placeName = "en la esquina";

    public string PlaceName => placeName;
    public Vector3 Center => transform.position;
}
