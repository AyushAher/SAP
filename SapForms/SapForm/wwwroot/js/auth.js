window.auth = {

    login: async function(username, password) {

        const response = await fetch('/api/auth/login', {
            method: 'POST',
            credentials: 'include', // VERY IMPORTANT
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                userName: username,
                password: password
            })
        });

        if (response.ok) {
            window.location.href = "/";
        } else {
            throw "Invalid credentials";
        }
    },

    logout: async function() {

        await fetch('/api/auth/logout', {
            method: 'POST',
            credentials: 'include'
        });

        window.location.href = "/Account/Login";
    },
    downloadFile: (filename, base64) => {
        const link = document.createElement('a');
        link.href = 'data:application/pdf;base64,' + base64;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }
};