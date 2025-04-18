// クライアントIDの生成と管理を行うスクリプト

// DOMの読み込みが完了したら実行
document.addEventListener('DOMContentLoaded', function() {
    console.log('ClientID manager loaded');
});

// クライアントIDを取得または新規作成する関数
window.getOrCreateClientId = function() {
    let clientId = localStorage.getItem('survey_client_id');
    
    // クライアントIDが存在しない場合は新規作成
    if (!clientId) {
        clientId = generateGuid();
        localStorage.setItem('survey_client_id', clientId);
        console.log('Generated new client ID: ' + clientId);
    } else {
        console.log('Using existing client ID: ' + clientId);
    }
    
    // BlazorコンポーネントにクライアントIDを設定
    DotNet.invokeMethodAsync('EsCQRSQuestions.Web', 'SetClientId', clientId);
    return clientId;
};

// GUIDを生成する関数
function generateGuid() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
        var r = Math.random() * 16 | 0;
        var v = c === 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

// クライアントIDをクリアする関数（テスト用）
window.clearClientId = function() {
    localStorage.removeItem('survey_client_id');
    console.log('Client ID cleared');
    return true;
};