
TA Network 스터디 - Ray Marching
===============================

이 저장소는 TA Network 스터디에서 Ray Marching 주제로 실습/논의을 위한 간단한 샘플을 포함하고 있습니다.

Ray Marching을 실행하기 위한 최소한의 렌더링 설정만을 포함하고 있기 때문에, 실제 상용 프로젝트에서 그대로 사용하는 것은 권장하지 않습니다.


시작하기 전에
------------

Ray Marching에 대해 논의하기 전에, 여러 가지 의미로 해석될 여지가 있거나, 다소 생소할 수 있는 기술 용어들을 미리 정의합니다.


- **Ray Marching**
  - /레이 마칭/으로 읽으며, SDF를 사용해서 거리를 측정하는 알고리즘을 의미합니다.
- **SDF**
  - Signed Distance Function 을 의미하며, 방향을 무시한 최단 거리 함수를 의미합니다. SDF 만으로는 특정 방향의 최단 거리는 알 수 없으며, Ray Marching을 거친 이후에 원하는 방향의 최단 거리를 근사할 수 있습니다.
- **Step**
  - Ray Marching 알고리즘 실행 과정 중, SDF를 최대로 실행하게 되는 횟수를 의미합니다. Step이 높을 수록 거리를 더욱 정확하게 근사할 수 있지만 비용도 함께 증가합니다.
- **월드 SDF / 또는 Scene SDF**
  - 하나 또는 여러 개의 SDF를 엮어서 월드 또는 Scene을 기술하는 SD 함수를 만들었을 때, 편의상 해당 함수를 월드 SDF라고 부르기로 합니다.
- **Lambert, Blinn-Phong**
  - Lambert는 Normal/Light 벡터의 단순 스칼라곱을 diffuse 반사로 사용하는 간단한 라이팅 모델입니다.
  - Blinn-Phong은 View/Normal 벡터의 반사 벡터 대신, 중간 벡터를 사용하는, Phong 라이팅 모델의 최적화된 버전입니다.
  - Phong은 View/Normal 벡터의 반사 벡터와 라이트 벡터의 스칼라곱을 specular 반사로 사용하는 간단한 라이팅 모델입니다.
  - 이 저장소에서 제시하는 예제들은 Lambert 또는 Blinn-Phong을 사용하고 있으며, PBR 라이팅 모델을 적용하는 것은 주제와 무관하고, 예제가 불필요하게 무거워질 수 있다고 생각해서 제외했습니다. Ray Marching 이후에 PBR을 적용하는 것은 그렇게 어렵지 않으니, 관심있는 분들은 따로 개인적으로 구현해보시기를 권장드립니다.
- **Reflectance**
  - Diffuse와 Specular, (예시에는 없지만) SSS 등 라이팅 모델의 연산 결과 반사되는 모든 것의 합을 의미합니다.
  - *Diffuse*
    - 난반사를 의미합니다. 관찰자의 시점과 무관하거나 또는 다른 벡터의 영향(Light 벡터 등)이 더 중요한 반사입니다. 표면에서 무작위로 반사되는 빛을 통계적으로 근사하는 반사입니다.
  - *Specular*
    - 정반사를 의미합니다. Normal 벡터에 의해 직접 반사된 빛을 근사합니다. 흔히 *하이라이트*라고 이야기하는 비쳐보이는 광원을 묘사합니다.
  - *SSS*
    - Sub-Surface Scattering을 의미합니다. 표면 아래에 일시적으로 흡수되었다가 표면 아래에서 다시 반사되어 나오는 빛을 묘사합니다. 예시에는 적용되어 있지 않습니다.
- **Boolean**
  - /불리언/으로 읽습니다. 여기서는 Polygon의 Boolean 연산 대신, 두 개의 SDF를 엮을 때 사용하는 함수를 의미합니다. Union, Intersection, Exclude 3 가지 Boolean을 사용합니다. 예제에서는 실시간 렌더링을 목적으로 하기 때문에, Polygon 렌더링에서 사용하는 Boolean 연산에 비해 훨씬 가벼운 근사 연산을 사용합니다.
    - *Union*
      - 두 SDF에서 표현하는 볼륨을 합친 볼륨을 표현합니다.
    - *Intersection*
      - 두 SDF에서 표현하는 볼륨의 교집합에 해당하는 볼륨을 표현합니다.
    - *Exclude*
      - 한 SDF에서 표현하는 볼륨으로부터 다른 하나의 SDF에서 표현하는 볼륨을 제외한 볼륨을 표현합니다.
- **Mix Function**
  - Boolean 연산에서 두 SDF의 경계면을 처리하는 방식을 표현하는 함수입니다.
    - 저장소에서 제시하는 예시에는 Chamfer, Round, Stairs 정도가 있지만 얼마든지 새로운 함수를 개발해 볼 수 있습니다.


참고한 문서
----------

[TA Study 문서](https://docs.google.com/document/d/1Nqh1-tDnixTG5-z5HM7-h3WI9gAJS7VtnoPm4ea8G5U)


결과물 미리 보기
---------------

![Pikachu](https://i.ibb.co/NjSpf6Q/image.png)
![Anim](https://i.ibb.co/KKbpWpP/image.png)
[![Unity Example](https://i.ibb.co/F6bRrf5/image.png)](http://minujeong.com/uweb/raymarching_in_unity_2/index.html)
![Translucent Volume](https://i.ibb.co/ZW68dV7/image.png)
[![Raymarching For Artists](https://i.ibb.co/2Nkmytc/image.png)](https://onedrive.live.com/view.aspx?resid=ACA530F7D253C02B!4125&ithint=file%2cpptx&authkey=!AHRn3NtbI-eMsVs)
