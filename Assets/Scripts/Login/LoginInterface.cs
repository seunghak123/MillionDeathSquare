using System;
using Cysharp.Threading.Tasks;

namespace Seunghak.LoginSystem
{
    /// <summary>
    /// 플랫폼별 로그인, 결제, 광고 기능을 정의하는 인터페이스
    /// </summary>
    public interface LoginInterface
    {
        #region 로그인
        /// <summary>
        /// 로그인 초기화
        /// </summary>
        void InitLogin();

        /// <summary>
        /// 플랫폼 로그인 실행
        /// </summary>
        /// <param name="successResultAct">성공 시 콜백 (토큰, 성공여부)</param>
        void PlatformLogin(Action<string, bool> successResultAct);
        #endregion

        #region 결제
        /// <summary>
        /// 상품 목록 가져오기
        /// </summary>
        /// <returns>상품 목록 배열</returns>
        UniTask<ProductInfo[]> GetProductListAsync();

        /// <summary>
        /// 상품 결제하기
        /// </summary>
        /// <param name="sku">상품 ID</param>
        /// <param name="onSuccess">성공 시 콜백 (주문 ID)</param>
        /// <param name="onError">실패 시 콜백 (에러 메시지)</param>
        void PurchaseProduct(string sku, Action<string> onSuccess, Action<string> onError);
        #endregion

        #region 광고
        /// <summary>
        /// 광고 기능 지원 여부 확인
        /// </summary>
        /// <returns>지원 여부</returns>
        bool IsAdSupported();

        /// <summary>
        /// 광고 불러오기
        /// </summary>
        /// <param name="adGroupId">광고 그룹 ID</param>
        /// <param name="onLoadComplete">로드 완료 콜백 (성공 여부)</param>
        void LoadAd(string adGroupId, Action<bool> onLoadComplete);

        /// <summary>
        /// 광고 표시하기
        /// </summary>
        /// <param name="adGroupId">광고 그룹 ID</param>
        /// <param name="onShowComplete">표시 완료 콜백 (성공 여부)</param>
        void ShowAd(string adGroupId, Action<bool> onShowComplete);
        #endregion
    }

    /// <summary>
    /// 상품 정보
    /// </summary>
    public class ProductInfo
    {
        public string sku;              // 상품 고유 ID
        public string displayAmount;    // 가격 (예: "1,000원")
        public string displayName;       // 상품 이름
        public string iconUrl;          // 아이콘 URL
        public string description;      // 상품 설명
    }
}
