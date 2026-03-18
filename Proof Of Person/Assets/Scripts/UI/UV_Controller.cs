using UnityEngine;

public class UV_Controller : MonoBehaviour
{

    [SerializeField] private GameObject UV_Image;

    public void UVOverlay()
    {

        if (UV_Image.activeSelf)
        {
            UV_Image.SetActive(false);
        }
        else
            UV_Image.SetActive(true);     
    }

}
