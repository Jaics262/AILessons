const childProcess = require('child_process');
const gulp = require('gulp');

function buildApi(done) {
    const proc = childProcess.spawn('dotnet', [
        'build',
        'swe.utils.api/swe.utils.api.csproj'
    ], {
        cwd: process.cwd()
    });

    proc.stdout.on('data', (data) => {
        console.log(data.toString());
    });

    proc.stderr.on('data', (data) => {
        console.log(data.toString());
    });

    proc.on('close', (code) => {
        if (code !== 0) {
            return done(new Error('Error building api project'));
        }

        return done();
    });
}

function startApi(done) {
    childProcess.exec(
        `pm2 start dotnet --name "swe.utils.api" --interpreter none -- swe.utils.api.dll --urls="http://localhost:5008"`,
        { cwd: "swe.utils.api/bin/Debug/net10.0" },
        function (err, stdout, stderr) {
            console.log(stdout);
            console.error(stderr);
            done(err);
        }
    );
}

function stopapi(done) {
    childProcess.exec('pm2 delete swe.utils.api', () => done());
}

gulp.task('stopapi', stopapi);
gulp.task('start:api', gulp.series(stopapi, buildApi, startApi));
